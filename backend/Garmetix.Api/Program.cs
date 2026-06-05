using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Api.Audit;
using Garmetix.Api.Auth;
using Garmetix.Api.Accounting;
using Garmetix.Api.Automation;
using Garmetix.Api.Billing;
using Garmetix.Api.Hr;
using Garmetix.Api.ImportExport;
using Garmetix.Api.Payroll;
using Garmetix.Api.Purchase;
using Garmetix.Api.Setup;
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is missing.");

builder.Services.AddGarmetixInfrastructure(connectionString);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<MonthlyAttendanceService>();
builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<AccountingPostingService>();
builder.Services.Configure<PayrollAutomationOptions>(builder.Configuration.GetSection("PayrollAutomation"));
builder.Services.AddHostedService<PayrollAutomationHostedService>();

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
    options.AddPolicy(GarmetixPolicies.Admin, policy => policy.RequireRole(GarmetixPolicies.AdminRoles));
    options.AddPolicy(GarmetixPolicies.CompanySetup, policy => policy.RequireRole(GarmetixPolicies.AdminRoles));
    options.AddPolicy(GarmetixPolicies.Billing, policy => policy.RequireRole(GarmetixPolicies.BillingRoles));
    options.AddPolicy(GarmetixPolicies.Inventory, policy => policy.RequireRole(GarmetixPolicies.InventoryRoles));
    options.AddPolicy(GarmetixPolicies.Purchase, policy => policy.RequireRole(GarmetixPolicies.InventoryRoles));
    options.AddPolicy(GarmetixPolicies.Accounting, policy => policy.RequireRole(GarmetixPolicies.AccountingRoles));
    options.AddPolicy(GarmetixPolicies.Hr, policy => policy.RequireRole(GarmetixPolicies.HrRoles));
    options.AddPolicy(GarmetixPolicies.Payroll, policy => policy.RequireRole(GarmetixPolicies.PayrollRoles));
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
    db.Database.Migrate();
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    name = "Garmetix API",
    modules = new[] { "Company", "Store", "Inventory", "Billing", "Purchase", "Voucher", "HR", "Payroll" }
}));

var auth = app.MapGroup("/api/auth").WithTags("Auth");
auth.MapGet("/bootstrap-status", BootstrapStatusAsync).AllowAnonymous();
auth.MapPost("/bootstrap-admin", BootstrapAdminAsync).AllowAnonymous();
auth.MapPost("/login", LoginAsync).AllowAnonymous();
auth.MapGet("/me", (HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken) =>
{
    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    return Guid.TryParse(userId, out var id)
        ? db.Users.AsNoTracking().Where(user => user.Id == id).Select(user => JwtTokenService.ToDto(user)).FirstOrDefaultAsync(cancellationToken)
        : Task.FromResult<AuthUserDto?>(null);
}).RequireAuthorization();

app.MapSetupEndpoints();
app.MapBillingEndpoints();
app.MapPurchaseEndpoints();
app.MapUserManagementEndpoints();
app.MapHrEndpoints();
app.MapPayrollEndpoints();
app.MapImportExportEndpoints();
app.MapAuditEndpoints();
app.MapAccountingEndpoints();

MapCrud<Company>(app, "/api/companies", GarmetixPolicies.CompanySetup);
MapCrud<StoreGroup>(app, "/api/store-groups", GarmetixPolicies.CompanySetup);
MapCrud<Store>(app, "/api/stores", GarmetixPolicies.CompanySetup);
MapCrud<Product>(app, "/api/products", GarmetixPolicies.Inventory);
MapCrud<Stock>(app, "/api/stocks", GarmetixPolicies.Inventory);
MapCrud<Customer>(app, "/api/customers", GarmetixPolicies.Billing);
MapCrud<Vendor>(app, "/api/vendors", GarmetixPolicies.Purchase);
MapCrud<Invoice>(app, "/api/sales-invoices", GarmetixPolicies.Billing);
MapCrud<PurchaseInvoice>(app, "/api/purchase-invoices", GarmetixPolicies.Purchase);
MapCrud<LedgerGroup>(app, "/api/ledger-groups", GarmetixPolicies.Accounting);
MapCrud<Ledger>(app, "/api/ledgers", GarmetixPolicies.Accounting);
MapCrud<Party>(app, "/api/parties", GarmetixPolicies.Accounting);
MapCrud<Bank>(app, "/api/banks", GarmetixPolicies.Accounting);
MapCrud<BankAccount>(app, "/api/bank-accounts", GarmetixPolicies.Accounting);
MapCrud<BankAccountDetail>(app, "/api/bank-account-details", GarmetixPolicies.Accounting);
MapCrud<VendorBankAccount>(app, "/api/vendor-bank-accounts", GarmetixPolicies.Accounting);
MapCrud<BankTransaction>(app, "/api/bank-transactions", GarmetixPolicies.Accounting);
MapCrud<ChequeLog>(app, "/api/cheque-logs", GarmetixPolicies.Accounting);
MapCrud<BankCashTranscation>(app, "/api/bank-cash-transactions", GarmetixPolicies.Accounting);
MapCrud<BankStatementLine>(app, "/api/bank-statement-lines", GarmetixPolicies.Accounting);
MapCrud<JournalEntry>(app, "/api/journal-entries", GarmetixPolicies.Accounting);
MapCrud<JournalLine>(app, "/api/journal-lines", GarmetixPolicies.Accounting);
MapCrud<Transaction>(app, "/api/transactions", GarmetixPolicies.Accounting);
MapCrud<PettyCashSheet>(app, "/api/petty-cash-sheets", GarmetixPolicies.Accounting);
MapCrud<DayBegin>(app, "/api/day-begins", GarmetixPolicies.Accounting);
MapCrud<DayEnd>(app, "/api/day-ends", GarmetixPolicies.Accounting);
MapCrud<Employee>(app, "/api/employees", GarmetixPolicies.Hr);
MapCrud<Attendance>(app, "/api/attendance", GarmetixPolicies.Hr);
MapCrud<MonthlyAttendance>(app, "/api/monthly-attendance", GarmetixPolicies.Hr);
MapCrud<SalaryStructure>(app, "/api/salary-structures", GarmetixPolicies.Payroll);
MapCrud<SalaryPaySlip>(app, "/api/salary-pay-slips", GarmetixPolicies.Payroll);
MapCrud<SalaryPayment>(app, "/api/salary-payments", GarmetixPolicies.Payroll);
MapCrud<AppUser>(app, "/api/users", GarmetixPolicies.Admin);

app.Run();

static RouteGroupBuilder MapCrud<T>(WebApplication app, string route, string policyName) where T : class, Garmetix.Core.Interfaces.IEntity
{
    var group = app.MapGroup(route).WithTags(typeof(T).Name).RequireAuthorization(policyName);

    group.MapGet("/", async (IGarmetixRepository repository, CancellationToken cancellationToken) =>
        Results.Ok(await repository.ListAsync<T>(cancellationToken)));
    group.MapGet("/{id:guid}", async (Guid id, IGarmetixRepository repository, CancellationToken cancellationToken) =>
        await repository.FindAsync<T>(id, cancellationToken) is { } entity ? Results.Ok(entity) : Results.NotFound());
    group.MapPost("/", async (T entity, IGarmetixRepository repository, CancellationToken cancellationToken) =>
        Results.Created($"{route}/{entity.Id}", await repository.SaveAsync(entity, cancellationToken)));
    group.MapPut("/{id:guid}", async (Guid id, T entity, IGarmetixRepository repository, CancellationToken cancellationToken) =>
    {
        entity.Id = id;
        return Results.Ok(await repository.SaveAsync(entity, cancellationToken));
    });
    group.MapDelete("/{id:guid}", async (Guid id, IGarmetixRepository repository, CancellationToken cancellationToken) =>
        await repository.DeleteAsync<T>(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

    return group;
}

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

    if (PasswordHasher.NeedsUpgrade(user.Password))
    {
        user.Password = PasswordHasher.Hash(request.Password);
        await db.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(tokens.CreateToken(user));
}
