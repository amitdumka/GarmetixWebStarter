using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Core.Models.Base;
using Garmetix.Api.Audit;
using Garmetix.Api.AppInfo;
using Garmetix.Api.Auth;
using Garmetix.Api.Attendance;
using Garmetix.Api.Attendance.Services;
using Garmetix.Api.Accounting;
using Garmetix.Api.Automation;
using Garmetix.Api.Backup;
using Garmetix.Api.Commercial;
using Garmetix.Api.Billing;
using Garmetix.Api.Database;
using Garmetix.Api.Dashboard;
using Garmetix.Api.Hr;
using Garmetix.Api.GstReturns;
using Garmetix.Api.Gstin;
using Garmetix.Api.ImportExport;
using Garmetix.Api.Licensing;
using Garmetix.Api.Messages;
using Garmetix.Api.Inventory;
using Garmetix.Api.OffBook;
using Garmetix.Api.Onboarding;
using Garmetix.Api.Numbering;
using Garmetix.Api.NonGstGoods;
using Garmetix.Api.Payroll;
using Garmetix.Api.Purchase;
using Garmetix.Api.ProductLookup;
using Garmetix.Api.Production;
using Garmetix.Api.Release;
using Garmetix.Api.Setup;
using Garmetix.Api.Tailoring;
using Garmetix.Api.Testing;
using Garmetix.Api.Seeds;
using Garmetix.Api.StoreDay;
using Garmetix.Api.Validation;
using Garmetix.Api.SecondarySync;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure;
using Garmetix.Infrastructure.Data;
using Garmetix.Models.DayOperations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var configuredCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var configuredCorsOriginsCsv = builder.Configuration["Cors:AllowedOriginsCsv"] ?? string.Empty;
var corsOrigins = configuredCorsOrigins
    .Concat(configuredCorsOriginsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (corsOrigins.Length == 0)
{
    corsOrigins = ["http://localhost:3000"];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is missing.");

builder.Services.AddGarmetixInfrastructure(connectionString);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<PasswordResetTokenService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<LicenseOptions>(builder.Configuration.GetSection("License"));
builder.Services.AddSingleton<LicenseActivationService>();
builder.Services.Configure<PasswordResetOptions>(builder.Configuration.GetSection("PasswordReset"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<PasswordResetEmailService>();
builder.Services.AddScoped<MonthlyAttendanceService>();
builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<IAttendanceRuleEngine, AttendanceRuleEngine>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IAttendanceSyncService, AttendanceSyncService>();
builder.Services.AddScoped<IBiometricEnrollmentService, BiometricEnrollmentService>();
builder.Services.AddScoped<IAttendancePhotoProofService, AttendancePhotoProofService>();
builder.Services.AddScoped<AccountingPostingService>();
builder.Services.AddScoped<DocumentNumberService>();
builder.Services.AddScoped<StockLedgerService>();
builder.Services.AddScoped<ApplicationMessageLogService>();
builder.Services.AddSingleton<PersistentApplicationLogQueue>();
builder.Services.AddSingleton<ILoggerProvider, PersistentApplicationLoggerProvider>();
builder.Services.AddHostedService<PersistentApplicationLogHostedService>();
builder.Services.Configure<PayrollAutomationOptions>(builder.Configuration.GetSection("PayrollAutomation"));
builder.Services.AddHostedService<PayrollAutomationHostedService>();
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection("Backup"));
builder.Services.Configure<GstinLookupOptions>(builder.Configuration.GetSection("GstinLookup"));
builder.Services.AddHttpClient<GstinLookupService>();
builder.Services.Configure<GoogleDriveBackupOptions>(builder.Configuration.GetSection("GoogleDriveBackup"));
builder.Services.AddHttpClient("GoogleDriveAuth");
builder.Services.AddHttpClient("GoogleDriveBackup");
builder.Services.AddSingleton<GoogleDriveBackupService>();
builder.Services.AddSingleton<DatabaseBackupService>();
builder.Services.AddHostedService<BackupAutomationHostedService>();
builder.Services.Configure<OracleSecondarySyncOptions>(builder.Configuration.GetSection("OracleSync"));
builder.Services.AddSingleton<OracleSecondarySyncService>();
builder.Services.AddHostedService<OracleSecondarySyncHostedService>();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Garmetix";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "GarmetixWeb";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    AddMatrixPolicy(options, GarmetixPolicies.Admin);
    AddMatrixPolicy(options, GarmetixPolicies.CompanySetup);
    AddMatrixPolicy(options, GarmetixPolicies.Edit);
    AddMatrixPolicy(options, GarmetixPolicies.Delete);
    AddMatrixPolicy(options, GarmetixPolicies.Billing);
    AddMatrixPolicy(options, GarmetixPolicies.Inventory);
    AddMatrixPolicy(options, GarmetixPolicies.Purchase);
    AddMatrixPolicy(options, GarmetixPolicies.Accounting);
    AddMatrixPolicy(options, GarmetixPolicies.Hr);
    AddMatrixPolicy(options, GarmetixPolicies.Payroll);
    AddMatrixPolicy(options, GarmetixPolicies.Attendance);
});

var app = builder.Build();

const string FreshSchemaBaselineMigrationId = "20260617000000_InitialFreshSchema";
const string FreshSchemaBaselineProductVersion = "10.0.8";

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        var schemaBootstrapMode = app.Configuration["Database:SchemaBootstrapMode"] ?? "Migrate";
        if (string.Equals(schemaBootstrapMode, "FreshBaseline", StringComparison.OrdinalIgnoreCase)
            || string.Equals(schemaBootstrapMode, "EnsureCreated", StringComparison.OrdinalIgnoreCase)
            || string.Equals(schemaBootstrapMode, "EnsureCreatedWithBaseline", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Creating database schema from current DbContext model using fresh baseline mode.");
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """);
            await db.Database.ExecuteSqlRawAsync($"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('{FreshSchemaBaselineMigrationId}', '{FreshSchemaBaselineProductVersion}')
                ON CONFLICT ("MigrationId") DO NOTHING;
                """);
        }
        else
        {
            db.Database.Migrate();
        }
    }

    await DatabaseSchemaRepairService.RepairKnownSchemaDriftAsync(db, logger);
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(self), microphone=(), geolocation=()");

    if (context.Request.IsHttps)
    {
        context.Response.Headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});

app.UseCors("frontend");
app.UseAuthentication();
app.UseMiddleware<AuditActorMiddleware>();
app.UseMiddleware<ActiveUserMiddleware>();
app.UseMiddleware<ApplicationMessageLogMiddleware>();
app.UseAuthorization();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseMiddleware<StoreDayGuardMiddleware>();
app.Use(async (context, next) =>
{
    var backupService = context.RequestServices.GetRequiredService<DatabaseBackupService>();
    if (backupService.IsRestoreInProgress
        && !context.Request.Path.StartsWithSegments("/api/health")
        && !context.Request.Path.StartsWithSegments("/api/backups/status")
        && !context.Request.Path.StartsWithSegments("/api/backups/cloud/status"))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Database restore is in progress. Please try again shortly."
        });
        return;
    }

    await next();
});

app.MapGet("/", () => Results.Ok(new
{
    name = "Garmetix API",
    modules = new[] { "Company", "Store", "Inventory", "Billing", "Purchase", "Parties", "Voucher", "Cash Voucher", "HR", "Payroll" }
}));
app.MapGet("/api/health", HealthAsync).AllowAnonymous();
app.MapPost("/api/database/repair", async (GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    await DatabaseSchemaRepairService.RepairKnownSchemaDriftAsync(db, loggerFactory.CreateLogger("DatabaseSchemaRepair"), cancellationToken);
    return Results.Ok(new { message = "Database schema repair completed.", completedAtUtc = DateTimeOffset.UtcNow });
}).RequireAuthorization(GarmetixPolicies.Admin);

var auth = app.MapGroup("/api/auth").WithTags("Auth");
auth.MapGet("/bootstrap-status", BootstrapStatusAsync).AllowAnonymous();
auth.MapPost("/bootstrap-admin", BootstrapAdminAsync).AllowAnonymous();
auth.MapPost("/login", LoginAsync).AllowAnonymous();
auth.MapPost("/forgot-password", ForgotPasswordAsync).AllowAnonymous();
auth.MapPost("/reset-password", ResetPasswordAsync).AllowAnonymous();
auth.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();
auth.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();
auth.MapPut("/me", UpdateCurrentUserProfileAsync).RequireAuthorization();

app.MapSetupEndpoints();
app.MapWorkspaceEndpoints();
app.MapStoreDayEndpoints();
app.MapCashDetailsEndpoints();
app.MapBillingEndpoints();
app.MapTailoringEndpoints();
app.MapPurchaseEndpoints();
app.MapVendorSettlementEndpoints();
app.MapUserManagementEndpoints();
app.MapAccessMatrixEndpoints();
app.MapHrEndpoints();
app.MapAttendanceEndpoints();
app.MapPayrollEndpoints();
app.MapSalaryPaymentEndpoints();
app.MapImportExportEndpoints();
app.MapAuditEndpoints();
app.MapAccountingEndpoints();
app.MapPettyCashEndpoints();
app.MapCashVoucherEndpoints();
app.MapBackupEndpoints();
app.MapFactoryResetEndpoints();
app.MapGstReturnEndpoints();
app.MapGstinEndpoints();
app.MapCommercialEndpoints();
app.MapProductLookupEndpoints();
app.MapInventoryProductMasterEndpoints();
app.MapInventoryStockOperationEndpoints();
app.MapInventoryStockReportEndpoints();
app.MapNonGstGoodsEndpoints();
app.MapOracleSecondarySyncEndpoints();
app.MapDataConsistencyEndpoints();
app.MapDataConsistencyRepairEndpoints();
app.MapDatabaseMigrationEndpoints();
app.MapDashboardEndpoints();
app.MapProductionReadinessEndpoints();
app.MapPrintAcceptanceEndpoints();
app.MapBarcodeAcceptanceEndpoints();
app.MapGstProductionAcceptanceEndpoints();
app.MapGoogleDriveBackupAcceptanceEndpoints();
app.MapAuditTrailFinalAcceptanceEndpoints();
app.MapStage10CompleteEndpoints();
app.MapRuntimeDiagnosticsEndpoints();
app.MapPermissionAcceptanceEndpoints();
app.MapStage10AFinalAcceptanceEndpoints();
app.MapEmailDeliveryDiagnosticsEndpoints();
app.MapLicenseEndpoints();
app.MapReleaseStabilizationEndpoints();
app.MapAfssSeederEndpoints();
app.MapPortableSeederEndpoints();
app.MapCompanyMergeEndpoints();
app.MapSeederVerificationEndpoints();
app.MapClientOnboardingEndpoints();
app.MapApplicationMessageLogEndpoints();
app.MapAppInfoEndpoints();
app.MapTestAutomationEndpoints();

MapCrud<Company>(app, "/api/companies", GarmetixPolicies.CompanySetup, readPolicyName: null);
MapCrud<StoreGroup>(app, "/api/store-groups", GarmetixPolicies.CompanySetup, readPolicyName: null);
MapCrud<Store>(app, "/api/stores", GarmetixPolicies.CompanySetup, readPolicyName: null);
MapCrud<Product>(app, "/api/products", GarmetixPolicies.Inventory);
MapCrud<Stock>(app, "/api/stocks", GarmetixPolicies.Inventory);
MapCrud<Garmetix.Core.Models.Inventory.ProductCategory>(app, "/api/product-categories", GarmetixPolicies.Inventory);
MapCrud<Garmetix.Core.Models.Inventory.ProductSubCategory>(app, "/api/product-sub-categories", GarmetixPolicies.Inventory);
MapCrud<ProductDetail>(app, "/api/product-details", GarmetixPolicies.Inventory);
MapCrud<Brand>(app, "/api/brands", GarmetixPolicies.Inventory);
MapCrud<Tax>(app, "/api/taxes", GarmetixPolicies.Inventory);
MapCrud<Customer>(app, "/api/customers", GarmetixPolicies.Billing);
MapCrud<Vendor>(app, "/api/vendors", GarmetixPolicies.Purchase);
MapCrud<Invoice>(app, "/api/sales-invoices", GarmetixPolicies.Billing);
MapCrud<PurchaseInvoice>(app, "/api/purchase-invoices", GarmetixPolicies.Purchase);
MapCrud<LedgerGroup>(app, "/api/ledger-groups", GarmetixPolicies.Accounting);
MapCrud<Ledger>(app, "/api/ledgers", GarmetixPolicies.Accounting);
MapCrud<Bank>(app, "/api/banks", GarmetixPolicies.Accounting);
MapCrud<BankAccountDetail>(app, "/api/bank-account-details", GarmetixPolicies.Accounting);
MapCrud<VendorBankAccount>(app, "/api/vendor-bank-accounts", GarmetixPolicies.Accounting);
MapCrud<BankTransaction>(app, "/api/bank-transactions", GarmetixPolicies.Accounting);
MapCrud<ChequeLog>(app, "/api/cheque-logs", GarmetixPolicies.Accounting);
MapCrud<BankCashTranscation>(app, "/api/bank-cash-transactions", GarmetixPolicies.Accounting);
MapCrud<BankStatementLine>(app, "/api/bank-statement-lines", GarmetixPolicies.Accounting);
MapCrud<JournalEntry>(app, "/api/journal-entries", GarmetixPolicies.Accounting);
MapCrud<JournalLine>(app, "/api/journal-lines", GarmetixPolicies.Accounting);
MapCrud<Transaction>(app, "/api/transactions", GarmetixPolicies.Accounting);
MapCrud<DayBegin>(app, "/api/day-begins", GarmetixPolicies.Accounting);
MapCrud<DayEnd>(app, "/api/day-ends", GarmetixPolicies.Accounting);
MapCrud<Employee>(app, "/api/employees", GarmetixPolicies.Hr);
MapCrud<Attendance>(app, "/api/attendance", GarmetixPolicies.Hr);
MapCrud<MonthlyAttendance>(app, "/api/monthly-attendance", GarmetixPolicies.Hr);
MapCrud<SalaryStructure>(app, "/api/salary-structures", GarmetixPolicies.Payroll);
MapCrud<SalaryPaySlip>(app, "/api/salary-pay-slips", GarmetixPolicies.Payroll);
MapCrud<AppUser>(app, "/api/users", GarmetixPolicies.Admin);

app.Run();

static RouteGroupBuilder MapCrud<T>(WebApplication app, string route, string policyName, string? readPolicyName = "") where T : class, Garmetix.Core.Interfaces.IEntity
{
    readPolicyName = readPolicyName == string.Empty ? policyName : readPolicyName;
    var group = app.MapGroup(route).WithTags(typeof(T).Name);

    var list = group.MapGet("/", async (GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken) =>
        Results.Ok(await WorkspaceScope.ApplyTo(db.Set<T>().AsNoTracking(), context).ToListAsync(cancellationToken)));

    var get = group.MapGet("/{id:guid}", async (Guid id, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken) =>
        await WorkspaceScope.ApplyTo(db.Set<T>().AsNoTracking(), context).FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken) is { } entity
            ? Results.Ok(entity)
            : Results.NotFound());

    if (readPolicyName is null)
    {
        list.RequireAuthorization();
        get.RequireAuthorization();
    }
    else
    {
        list.RequireAuthorization(readPolicyName);
        get.RequireAuthorization(readPolicyName);
    }

    group.MapPost("/", async (T entity, GarmetixDbContext db, HttpContext context, GstinLookupService gstinLookup, CancellationToken cancellationToken) =>
    {
        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            return Results.BadRequest(new { message });
        }

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        await EnrichPartyGstinAsync(entity, gstinLookup, cancellationToken);
        var employeeValidationMessage = await PrepareEmployeeMasterAsync(entity, db, cancellationToken);
        if (employeeValidationMessage is not null)
        {
            return Results.BadRequest(new { message = employeeValidationMessage });
        }

        var duplicateDailyMessage = await EnsureUniqueDailyRecordAsync(entity, db, cancellationToken);
        if (duplicateDailyMessage is not null)
        {
            return Results.BadRequest(new { message = duplicateDailyMessage });
        }

        db.Set<T>().Add(entity);
        await SyncEmployeeSalesmanAsync(entity, db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (entity is Company company)
        {
            var defaultSeeder = new AfssDefaultSeederService(db);
            await defaultSeeder.SeedAccountingDefaultsForCompanyAsync(company.Id, cancellationToken);
        }

        return Results.Created($"{route}/{entity.Id}", entity);
    }).RequireAuthorization(policyName);

    group.MapPut("/{id:guid}", async (Guid id, T entity, GarmetixDbContext db, HttpContext context, GstinLookupService gstinLookup, CancellationToken cancellationToken) =>
    {
        entity.Id = id;
        if (!await WorkspaceScope.ApplyTo(db.Set<T>().AsNoTracking(), context).AnyAsync(item => item.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            return Results.BadRequest(new { message });
        }

        await EnrichPartyGstinAsync(entity, gstinLookup, cancellationToken);
        var employeeValidationMessage = await PrepareEmployeeMasterAsync(entity, db, cancellationToken);
        if (employeeValidationMessage is not null)
        {
            return Results.BadRequest(new { message = employeeValidationMessage });
        }

        var duplicateDailyMessage = await EnsureUniqueDailyRecordAsync(entity, db, cancellationToken);
        if (duplicateDailyMessage is not null)
        {
            return Results.BadRequest(new { message = duplicateDailyMessage });
        }

        db.Entry(entity).State = EntityState.Modified;
        await SyncEmployeeSalesmanAsync(entity, db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(entity);
    }).RequireAuthorization(policyName).RequireAuthorization(GarmetixPolicies.Edit);

    group.MapDelete("/{id:guid}", async (Guid id, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken) =>
    {
        var entity = await WorkspaceScope.ApplyTo(db.Set<T>(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        if (entity is LedgerGroup ledgerGroup && AccountingDefaultProtection.IsProtectedLedgerGroup(ledgerGroup))
        {
            return Results.BadRequest(new { message = "Default Indian accounting ledger groups are protected and cannot be deleted." });
        }

        if (entity is Ledger ledger && AccountingDefaultProtection.IsProtectedLedger(ledger))
        {
            return Results.BadRequest(new { message = "Default Indian accounting ledgers are protected and cannot be deleted." });
        }

        await ApplyCascadeSoftDeleteAsync(entity, db, cancellationToken);

        if (entity is BaseEntity softDeletable)
        {
            softDeletable.Deleted = true;
            db.Entry(entity).State = EntityState.Modified;
        }
        else
        {
            db.Remove(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    })
        .RequireAuthorization(policyName)
        .RequireAuthorization(GarmetixPolicies.Delete);

    return group;
}

static async Task<string?> PrepareEmployeeMasterAsync<T>(T entity, GarmetixDbContext db, CancellationToken cancellationToken) where T : class
{
    if (entity is not Employee employee)
    {
        return null;
    }

    employee.FirstName = (employee.FirstName ?? string.Empty).Trim();
    employee.LastName = (employee.LastName ?? string.Empty).Trim();
    employee.Mobile = DigitsOnly(employee.Mobile);
    employee.Aadhar = DigitsOnly(employee.Aadhar);
    employee.PAN = string.IsNullOrWhiteSpace(employee.PAN) ? null : employee.PAN.Trim().ToUpperInvariant();
    employee.IFSC = string.IsNullOrWhiteSpace(employee.IFSC) ? null : employee.IFSC.Trim().ToUpperInvariant();
    employee.EmployeeStatus = string.IsNullOrWhiteSpace(employee.EmployeeStatus)
        ? (employee.Working ? "Active" : "Inactive")
        : employee.EmployeeStatus.Trim();
    employee.SalaryType = string.IsNullOrWhiteSpace(employee.SalaryType) ? "Monthly" : employee.SalaryType.Trim();
    employee.Department = string.IsNullOrWhiteSpace(employee.Department) ? null : employee.Department.Trim();
    employee.Designation = string.IsNullOrWhiteSpace(employee.Designation) ? null : employee.Designation.Trim();
    employee.FatherOrHusbandName = string.IsNullOrWhiteSpace(employee.FatherOrHusbandName) ? null : employee.FatherOrHusbandName.Trim();
    employee.BankAccountNumber = string.IsNullOrWhiteSpace(employee.BankAccountNumber) ? null : DigitsOnly(employee.BankAccountNumber);
    employee.EmergencyContact = string.IsNullOrWhiteSpace(employee.EmergencyContact) ? null : employee.EmergencyContact.Trim();
    employee.UpdatedAt = DateTime.UtcNow;

    if (string.IsNullOrWhiteSpace(employee.FirstName) || string.IsNullOrWhiteSpace(employee.LastName))
    {
        return "Employee first name and last name are required.";
    }

    if (employee.Aadhar.Length != 12)
    {
        return "Aadhaar number must be exactly 12 digits.";
    }

    if (!string.IsNullOrWhiteSpace(employee.PAN) && employee.PAN.Length != 10)
    {
        return "PAN number must be exactly 10 characters.";
    }

    if (employee.Mobile.Length < 10 || employee.Mobile.Length > 15)
    {
        return "Mobile number must be 10 to 15 digits.";
    }

    if (employee.MonthlySalary < 0 || employee.DailyWage < 0)
    {
        return "Salary and wage values cannot be negative.";
    }

    if (employee.EmpId <= 0)
    {
        var maxExistingEmpId = await db.Employees.AsNoTracking()
            .Where(item => item.CompanyId == employee.CompanyId && item.StoreId == employee.StoreId && item.Id != employee.Id)
            .Select(item => (int?)item.EmpId)
            .MaxAsync(cancellationToken) ?? 0;

        employee.EmpId = maxExistingEmpId + 1;
    }

    if (string.IsNullOrWhiteSpace(employee.EmployeeCode))
    {
        employee.EmployeeCode = $"EMP-{employee.EmpId:0000}";
    }
    else
    {
        employee.EmployeeCode = employee.EmployeeCode.Trim().ToUpperInvariant();
    }

    if (employee.EmployeeStatus.Equals("Resigned", StringComparison.OrdinalIgnoreCase)
        || employee.EmployeeStatus.Equals("Terminated", StringComparison.OrdinalIgnoreCase)
        || employee.EmployeeStatus.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
    {
        employee.Working = false;
        employee.LeavingDate ??= DateTime.Today;
    }
    else
    {
        employee.Working = true;
        if (employee.EmployeeStatus.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            employee.LeavingDate = null;
            employee.ExitReason = null;
        }
    }

    return null;
}

static async Task SyncEmployeeSalesmanAsync<T>(T entity, GarmetixDbContext db, CancellationToken cancellationToken) where T : class
{
    if (entity is not Employee employee)
    {
        return;
    }

    var salesman = await db.Salesmen
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(item => item.EmployeeId == employee.Id, cancellationToken);

    if (employee.Category != EmployeeCategory.Salesman || !employee.Working || employee.Deleted)
    {
        if (salesman is not null)
        {
            salesman.Active = false;
            salesman.UpdatedAt = DateTime.UtcNow;
        }

        return;
    }

    if (salesman is null)
    {
        salesman = new Salesman
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id
        };
        db.Salesmen.Add(salesman);
    }

    var salesmanName = string.IsNullOrWhiteSpace(employee.StaffName)
        ? $"{employee.FirstName} {employee.LastName}".Trim()
        : employee.StaffName.Trim();

    salesman.CompanyId = employee.CompanyId;
    salesman.StoreGroupId = employee.StoreGroupId;
    salesman.StoreId = employee.StoreId;
    salesman.Name = string.IsNullOrWhiteSpace(salesmanName) ? "Salesman" : salesmanName;
    salesman.EmployeeId = employee.Id;
    salesman.Active = true;
    salesman.Deleted = false;
    salesman.UpdatedAt = DateTime.UtcNow;
}

static string DigitsOnly(string? value)
    => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

static async Task<string?> EnsureUniqueDailyRecordAsync<T>(T entity, GarmetixDbContext db, CancellationToken cancellationToken) where T : class
{
    switch (entity)
    {
        case Attendance attendance:
        {
            attendance.OnDate = attendance.OnDate.Date;
            var duplicate = await db.Attendance.AsNoTracking()
                .AnyAsync(item => item.EmployeeId == attendance.EmployeeId
                    && item.OnDate == attendance.OnDate
                    && item.Id != attendance.Id
                    && !item.Deleted, cancellationToken);
            return duplicate ? "Attendance already exists for this employee and date." : null;
        }
        case PettyCashSheet sheet:
        {
            sheet.OnDate = sheet.OnDate.Date;
            var duplicate = await db.PettyCashSheets.AsNoTracking()
                .AnyAsync(item => item.StoreId == sheet.StoreId
                    && item.OnDate == sheet.OnDate
                    && item.Id != sheet.Id
                    && !item.Deleted, cancellationToken);
            return duplicate ? "Petty cash sheet already exists for this store and date." : null;
        }
        default:
            return null;
    }
}

static async Task ApplyCascadeSoftDeleteAsync<T>(T entity, GarmetixDbContext db, CancellationToken cancellationToken) where T : class
{
    if (entity is Company company)
    {
        await SoftDeleteByScopeColumnAsync(db, "CompanyId", company.Id, cancellationToken);
    }
    else if (entity is StoreGroup storeGroup)
    {
        await SoftDeleteByScopeColumnAsync(db, "StoreGroupId", storeGroup.Id, cancellationToken);
    }
    else if (entity is Store store)
    {
        await SoftDeleteByScopeColumnAsync(db, "StoreId", store.Id, cancellationToken);
    }
}

static async Task SoftDeleteByScopeColumnAsync(GarmetixDbContext db, string columnName, Guid scopeId, CancellationToken cancellationToken)
{
    foreach (var entityType in db.Model.GetEntityTypes())
    {
        if (entityType.FindProperty("Deleted") is null || entityType.FindProperty(columnName) is null)
        {
            continue;
        }

        var table = entityType.GetTableName();
        if (string.IsNullOrWhiteSpace(table))
        {
            continue;
        }

        var schema = entityType.GetSchema();
        var qualifiedTable = string.IsNullOrWhiteSpace(schema) || string.Equals(schema, "public", StringComparison.OrdinalIgnoreCase)
            ? QuoteIdentifier(table)
            : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(table)}";

        var sql = $"UPDATE {qualifiedTable} SET {QuoteIdentifier("Deleted")} = TRUE WHERE {QuoteIdentifier(columnName)} = {{0}}";
        await db.Database.ExecuteSqlRawAsync(sql, new object[] { scopeId }, cancellationToken);
    }
}

static string QuoteIdentifier(string identifier)
    => $"\"{identifier.Replace("\"", "\"\"")}\"";

static async Task EnrichPartyGstinAsync<T>(T entity, GstinLookupService gstinLookup, CancellationToken cancellationToken) where T : class
{
    switch (entity)
    {
        case Customer customer when !string.IsNullOrWhiteSpace(customer.GSTIN):
        {
            var validation = await gstinLookup.ValidatePartyAsync("Customer", customer.GSTIN, customer.Name, customer.Address, cancellationToken);
            gstinLookup.ApplyVerification(customer, validation);
            break;
        }
        case Vendor vendor when !string.IsNullOrWhiteSpace(vendor.GSTIN):
        {
            var validation = await gstinLookup.ValidatePartyAsync("Vendor", vendor.GSTIN, vendor.Name, vendor.Address, cancellationToken);
            gstinLookup.ApplyVerification(vendor, validation);
            break;
        }
    }
}

static void AddMatrixPolicy(AuthorizationOptions options, string policyName)
    => options.AddPolicy(
        policyName,
        policy => policy.RequireAssertion(context => AccessPermissionMatrix.CanAccessPolicy(context.User, policyName)));

static async Task<IResult> BootstrapAdminAsync(BootstrapAdminRequest request, GarmetixDbContext db, JwtTokenService tokens, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Username and password are required." });
    }

    if (await db.Users.AnyAsync(user => user.Admin || user.Role == LoginRole.Admin, cancellationToken))
    {
        return Results.Conflict(new { message = "Bootstrap is available only before the first admin exists." });
    }

    var user = new AppUser
    {
        Name = request.Name.Trim(),
        UserName = request.UserName.Trim(),
        Email = request.Email.Trim(),
        Password = PasswordHasher.Hash(request.Password),
        Role = LoginRole.Admin,
        UserType = UserType.Admin,
        Admin = true,
        AppOperation = AppOperation.All
    };

    db.Users.Add(user);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/users/{user.Id}", tokens.CreateToken(user));
}

static async Task<IResult> HealthAsync(GarmetixDbContext db, IWebHostEnvironment environment, CancellationToken cancellationToken)
{
    try
    {
        var databaseReady = await db.Database.CanConnectAsync(cancellationToken);
        return Results.Ok(new
        {
            status = databaseReady ? "Healthy" : "Database unavailable",
            application = "Garmetix API",
            environment = environment.EnvironmentName,
            databaseReady,
            checkedAtUtc = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            status = "Database unavailable",
            application = "Garmetix API",
            environment = environment.EnvironmentName,
            databaseReady = false,
            checkedAtUtc = DateTimeOffset.UtcNow,
            message = ex.Message
        });
    }
}

static async Task<IResult> BootstrapStatusAsync(GarmetixDbContext db, CancellationToken cancellationToken)
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            return Results.Ok(new BootstrapStatusResponse(false, false, false, "Database connection is not available."));
        }

        var hasUsers = await db.Users.AnyAsync(cancellationToken);
        var hasAdmin = await db.Users.AnyAsync(user => user.Admin || user.Role == LoginRole.Admin, cancellationToken);
        return Results.Ok(new BootstrapStatusResponse(
            true,
            hasUsers,
            hasAdmin,
            hasAdmin ? "Admin user exists. Use login." : "No admin user found. Create the first admin."));
    }
    catch (Exception ex)
    {
        return Results.Ok(new BootstrapStatusResponse(false, false, false, ex.Message));
    }
}


static async Task<IResult> ForgotPasswordAsync(
    ForgotPasswordRequest request,
    GarmetixDbContext db,
    PasswordResetTokenService resetTokens,
    PasswordResetEmailService resetEmail,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.UserNameOrEmail))
    {
        return Results.BadRequest(new { message = "Username or email is required." });
    }

    var lookup = request.UserNameOrEmail.Trim();
    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(
        item => item.UserName == lookup || item.Email == lookup,
        cancellationToken);

    // Keep the normal response generic so the login screen does not reveal whether a user exists.
    const string genericMessage = "If the account exists, a password reset link has been sent to the registered email address.";
    if (user is null || !user.IsActive)
    {
        return Results.Ok(new ForgotPasswordResponse(genericMessage, null, null, null));
    }

    var now = UtcNowForStorage();
    var token = resetTokens.CreateToken(user.Id);
    var expiresAtUtc = DateTime.SpecifyKind(resetTokens.ExpiresAtUtc, DateTimeKind.Unspecified);
    var resetUrl = BuildPasswordResetUrl(configuration, httpContext, token);
    var tokenHash = resetTokens.HashToken(token);

    await RevokeActivePasswordResetTokensAsync(db, user.Id, now, cancellationToken);
    db.PasswordResetTokens.Add(new PasswordResetToken
    {
        UserId = user.Id,
        TokenHash = tokenHash,
        CreatedAtUtc = now,
        ExpiresAtUtc = expiresAtUtc,
        RequestIpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
        RequestUserAgent = httpContext.Request.Headers.UserAgent.ToString()
    });
    await db.SaveChangesAsync(cancellationToken);

    if (resetEmail.IsEnabled)
    {
        try
        {
            await resetEmail.SendAsync(user, resetUrl, token, expiresAtUtc, cancellationToken);
            return Results.Ok(new ForgotPasswordResponse(genericMessage, null, null, expiresAtUtc));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email for user {UserId}.", user.Id);

            if (!environment.IsDevelopment())
            {
                return Results.Ok(new ForgotPasswordResponse(genericMessage, null, null, expiresAtUtc));
            }
        }
    }

    if (environment.IsDevelopment())
    {
        return Results.Ok(new ForgotPasswordResponse(
            "Development reset token generated. Configure Email:Enabled=true to send reset email through SMTP.",
            token,
            resetUrl,
            expiresAtUtc));
    }

    logger.LogWarning("Password reset email was requested for user {UserId}, but Email:Enabled is false.", user.Id);
    return Results.Ok(new ForgotPasswordResponse(
        "Password reset email is not configured on this server. Ask an admin/owner to configure SMTP or reset the password from Roles & Users.",
        null,
        null,
        expiresAtUtc));
}

static string BuildPasswordResetUrl(IConfiguration configuration, HttpContext httpContext, string token)
{
    var frontendBaseUrl = configuration["PasswordReset:FrontendBaseUrl"];
    if (string.IsNullOrWhiteSpace(frontendBaseUrl))
    {
        frontendBaseUrl = httpContext.Request.Headers.Origin.ToString();
    }

    if (string.IsNullOrWhiteSpace(frontendBaseUrl))
    {
        frontendBaseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
    }

    return $"{frontendBaseUrl.TrimEnd('/')}/?token={Uri.EscapeDataString(token)}";
}

static async Task<IResult> ResetPasswordAsync(
    ResetPasswordRequest request,
    GarmetixDbContext db,
    PasswordResetTokenService resetTokens,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
    {
        return Results.BadRequest(new { message = "New password must be at least 6 characters." });
    }

    if (!resetTokens.TryValidate(request.Token, out var userId, out var tokenMessage))
    {
        return Results.BadRequest(new { message = tokenMessage });
    }

    var now = UtcNowForStorage();
    var tokenHash = resetTokens.HashToken(request.Token);
    var storedToken = await db.PasswordResetTokens.FirstOrDefaultAsync(
        token => token.UserId == userId && token.TokenHash == tokenHash,
        cancellationToken);

    if (storedToken is null)
    {
        return Results.BadRequest(new { message = "Reset token is invalid, expired, or has already been revoked." });
    }

    if (storedToken.UsedAtUtc.HasValue)
    {
        return Results.BadRequest(new { message = "Reset token has already been used. Request a new reset link." });
    }

    if (storedToken.RevokedAtUtc.HasValue)
    {
        return Results.BadRequest(new { message = "Reset token has been revoked. Request a new reset link." });
    }

    if (storedToken.ExpiresAtUtc < now)
    {
        storedToken.RevokedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return Results.BadRequest(new { message = "Reset token has expired. Request a new reset link." });
    }

    var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
    if (user is null)
    {
        storedToken.RevokedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return Results.BadRequest(new { message = "Reset token user was not found." });
    }

    if (!user.IsActive)
    {
        storedToken.RevokedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return Results.BadRequest(new { message = "This account is inactive. Contact an Owner or Admin." });
    }

    user.Password = PasswordHasher.Hash(request.NewPassword);
    storedToken.UsedAtUtc = now;
    await RevokeActivePasswordResetTokensAsync(db, user.Id, now, cancellationToken, exceptTokenId: storedToken.Id);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new { message = "Password reset successfully. You can login with the new password." });
}


static async Task<IResult> GetCurrentUserAsync(
    HttpContext context,
    GarmetixDbContext db,
    CancellationToken cancellationToken)
{
    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userId, out var id))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    return user is null
        ? Results.NotFound(new { message = "Current user was not found." })
        : Results.Ok(JwtTokenService.ToDto(user));
}

static async Task<IResult> UpdateCurrentUserProfileAsync(
    UpdateProfileRequest request,
    HttpContext context,
    GarmetixDbContext db,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.Name)
        || string.IsNullOrWhiteSpace(request.UserName)
        || string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new { message = "Name, username, and email are required." });
    }

    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userId, out var id))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (user is null)
    {
        return Results.NotFound(new { message = "Current user was not found." });
    }

    var normalizedUserName = request.UserName.Trim();
    var normalizedEmail = request.Email.Trim();
    var exists = await db.Users.AnyAsync(
        item => item.Id != id && (item.UserName == normalizedUserName || item.Email == normalizedEmail),
        cancellationToken);

    if (exists)
    {
        return Results.Conflict(new { message = "A user with the same username or email already exists." });
    }

    user.Name = request.Name.Trim();
    user.UserName = normalizedUserName;
    user.Email = normalizedEmail;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(JwtTokenService.ToDto(user));
}

static async Task<IResult> ChangePasswordAsync(
    ChangePasswordRequest request,
    HttpContext context,
    GarmetixDbContext db,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
    {
        return Results.BadRequest(new { message = "New password must be at least 6 characters." });
    }

    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userId, out var id))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (user is null)
    {
        return Results.NotFound(new { message = "Current user was not found." });
    }

    if (!PasswordHasher.Verify(request.CurrentPassword, user.Password))
    {
        return Results.BadRequest(new { message = "Current password is incorrect." });
    }

    user.Password = PasswordHasher.Hash(request.NewPassword);
    await RevokeActivePasswordResetTokensAsync(db, user.Id, UtcNowForStorage(), cancellationToken);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new { message = "Password changed successfully." });
}


static DateTime UtcNowForStorage() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

static async Task RevokeActivePasswordResetTokensAsync(
    GarmetixDbContext db,
    Guid userId,
    DateTime revokedAtUtc,
    CancellationToken cancellationToken,
    Guid? exceptTokenId = null)
{
    var activeTokens = await db.PasswordResetTokens
        .Where(token => token.UserId == userId
            && token.UsedAtUtc == null
            && token.RevokedAtUtc == null
            && token.Id != exceptTokenId)
        .ToListAsync(cancellationToken);

    foreach (var activeToken in activeTokens)
    {
        activeToken.RevokedAtUtc = revokedAtUtc;
    }
}

static async Task<IResult> LoginAsync(LoginRequest request, GarmetixDbContext db, JwtTokenService tokens, CancellationToken cancellationToken)
{
    var normalizedUserName = request.UserName.Trim();
    var user = await db.Users.FirstOrDefaultAsync(
        item => item.UserName == normalizedUserName || item.Email == normalizedUserName,
        cancellationToken);

    if (user is null || !PasswordHasher.Verify(request.Password, user.Password))
    {
        return Results.Unauthorized();
    }

    if (!user.IsActive)
    {
        return Results.Json(
            new { message = "This account is inactive. Contact an Owner or Admin." },
            statusCode: StatusCodes.Status403Forbidden);
    }

    if (PasswordHasher.NeedsUpgrade(user.Password))
    {
        user.Password = PasswordHasher.Hash(request.Password);
        await db.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(tokens.CreateToken(user));
}
