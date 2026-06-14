using System.Globalization;
using System.Text;
using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Garmetix.Models.DayOperations;
using Microsoft.EntityFrameworkCore;
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
using InventoryProductSubCategory = Garmetix.Core.Models.Inventory.ProductSubCategory;

namespace Garmetix.Api.ImportExport;

public static class ImportExportEndpoints
{
    private static readonly Dictionary<string, ExportDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["setup"] = new(
            "Company",
            ["Type", "CompanyCode", "StoreGroupCode", "Name", "Code", "Contact", "Email", "City", "State", "Active"],
            async (db, cancellationToken) =>
            {
                var companies = await db.Companies.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
                var groups = await db.StoreGroups.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
                var stores = await db.Stores.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
                var companyCodes = companies.ToDictionary(item => item.Id, item => item.Code);
                var groupCodes = groups.ToDictionary(item => item.Id, item => item.GroupCode);

                return companies.Select(item => Row("Company", item.Code, "", item.Name, item.Code, item.ContactNumber, item.Email, item.City, item.State, item.Active))
                    .Concat(groups.Select(item => Row("StoreGroup", companyCodes.GetValueOrDefault(item.CompanyId), item.GroupCode, item.Name, item.GroupCode, "", "", "", "", item.Active)))
                    .Concat(stores.Select(item => Row("Store", companyCodes.GetValueOrDefault(item.CompanyId), groupCodes.GetValueOrDefault(item.StoreGroupId), item.Name, item.StoreCode, item.ContactNumber, item.Email, item.City, item.State, item.Active)));
            }),
        ["inventory"] = new(
            "Inventory",
            ["Product", "Barcode", "MRP", "TaxRate", "Unit", "ProductType", "PurchasedQty", "SoldQty", "CurrentStock", "CostPrice"],
            async (db, cancellationToken) =>
            {
                var rows = await db.Products.AsNoTracking()
                    .Where(product =>
                        !db.Stocks.Any(stock => stock.ProductId == product.Id) ||
                        db.Stocks.Any(stock => stock.ProductId == product.Id && !stock.IsOFB))
                    .GroupJoin(
                        db.Stocks.AsNoTracking().Where(stock => !stock.IsOFB),
                        product => product.Id,
                        stock => stock.ProductId,
                        (product, stocks) => new { product, stocks })
                    .SelectMany(
                        item => item.stocks.DefaultIfEmpty(),
                        (item, stock) => new
                        {
                            item.product.Name,
                            item.product.Barcode,
                            item.product.MRP,
                            item.product.TaxRate,
                            item.product.Unit,
                            item.product.ProductType,
                            PurchaseQty = stock == null ? 0 : stock.PurchaseQty,
                            SoldQty = stock == null ? 0 : stock.SoldQty,
                            CostPrice = stock == null ? 0 : stock.CostPrice
                        })
                    .OrderBy(item => item.Name)
                    .ToListAsync(cancellationToken);

                return rows.Select(item => Row(
                    item.Name,
                    item.Barcode,
                    item.MRP,
                    item.TaxRate,
                    item.Unit,
                    item.ProductType,
                    item.PurchaseQty,
                    item.SoldQty,
                    item.PurchaseQty - item.SoldQty,
                    item.CostPrice));
            }),
        ["billing"] = new(
            "Billing",
            ["InvoiceNumber", "Date", "Customer", "Mobile", "Product", "Barcode", "Quantity", "MRP", "Discount", "Paid", "PaymentMode", "BankAccountNumber", "BillDiscount", "CompanyCode", "StoreGroupCode", "StoreCode"],
            async (db, cancellationToken) =>
            {
                var invoices = await db.SalesInvoices.AsNoTracking()
                    .OrderByDescending(item => item.OnDate)
                    .ToListAsync(cancellationToken);
                var invoiceIds = invoices.Select(item => item.Id).ToList();
                var items = await db.InvoiceItems.AsNoTracking()
                    .Where(item => invoiceIds.Contains(item.InvoiceId))
                    .ToListAsync(cancellationToken);
                var products = await db.Products.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
                var stores = await db.Stores.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
                var groups = await db.StoreGroups.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
                var companies = await db.Companies.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);

                return invoices.SelectMany(invoice =>
                {
                    var invoiceItems = items.Where(item => item.InvoiceId == invoice.Id).DefaultIfEmpty();
                    return invoiceItems.Select(item =>
                    {
                        products.TryGetValue(item?.ProductId ?? Guid.Empty, out var product);
                        stores.TryGetValue(invoice.StoreId, out var store);
                        groups.TryGetValue(store?.StoreGroupId ?? Guid.Empty, out var group);
                        companies.TryGetValue(invoice.CompanyId, out var company);
                        return Row(
                            invoice.InvoiceNumber,
                            invoice.OnDate,
                            invoice.CustomerName,
                            invoice.CustomerMobileNumber,
                            product?.Name ?? "",
                            item?.Barcode ?? "",
                            item?.BilledQuantity ?? invoice.Quantity,
                            item?.MRP ?? invoice.MRP,
                            item?.DiscountAmount ?? invoice.DiscountAmount,
                            invoice.PaidAmount,
                            invoice.PaymentMode,
                            "",
                            invoice.BillDiscountAmount,
                            company?.Code ?? "",
                            group?.GroupCode ?? "",
                            store?.StoreCode ?? "");
                    });
                });
            }),
        ["purchase"] = new(
            "Purchase",
            ["InvoiceNumber", "InwardNumber", "Date", "Vendor", "VendorMobile", "GSTIN", "Product", "Barcode", "Quantity", "CostPrice", "MRP", "Discount", "TaxRate", "Paid", "PaymentMode", "BankAccountNumber", "FrightAmount", "CompanyCode", "StoreGroupCode", "StoreCode"],
            async (db, cancellationToken) =>
            {
                var invoices = await db.PurchaseInvoices.AsNoTracking()
                    .OrderByDescending(item => item.OnDate)
                    .ToListAsync(cancellationToken);
                var invoiceIds = invoices.Select(item => item.Id).ToList();
                var items = await db.PurchaseInvoiceItems.AsNoTracking()
                    .Where(item => invoiceIds.Contains(item.InvoiceId))
                    .ToListAsync(cancellationToken);
                var products = await db.Products.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
                var stores = await db.Stores.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
                var groups = await db.StoreGroups.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
                var companies = await db.Companies.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);

                return invoices.SelectMany(invoice =>
                {
                    var invoiceItems = items.Where(item => item.InvoiceId == invoice.Id).DefaultIfEmpty();
                    return invoiceItems.Select(item =>
                    {
                        products.TryGetValue(item?.ProductId ?? Guid.Empty, out var product);
                        var store = stores.Values.FirstOrDefault(item => item.CompanyId == invoice.CompanyId);
                        groups.TryGetValue(store?.StoreGroupId ?? Guid.Empty, out var group);
                        companies.TryGetValue(invoice.CompanyId, out var company);
                        return Row(
                            invoice.InvoiceNumber,
                            invoice.InwardNumber,
                            invoice.OnDate,
                            invoice.VendorName,
                            "",
                            invoice.VendorGSTIN,
                            product?.Name ?? "",
                            item?.Barcode ?? "",
                            item?.BilledQuantity ?? invoice.Quantity,
                            item is null || item.BilledQuantity == 0 ? 0 : Math.Round(item.Amount / item.BilledQuantity, 2),
                            item?.MRP ?? invoice.MRP,
                            item?.DiscountAmount ?? invoice.DiscountAmount,
                            item?.TaxPercentage ?? 0,
                            0,
                            invoice.PaymentMode,
                            "",
                            invoice.FrightAmount,
                            company?.Code ?? "",
                            group?.GroupCode ?? "",
                            store?.StoreCode ?? "");
                    });
                });
            }),
        ["vouchers"] = new(
            "Vouchers",
            ["VoucherNumber", "Date", "Type", "Party", "Particulars", "PaymentMode", "Amount", "Remarks"],
            async (db, cancellationToken) =>
            {
                var rows = await db.Vouchers.AsNoTracking()
                    .OrderByDescending(item => item.OnDate)
                    .ToListAsync(cancellationToken);

                return rows.Select(item => Row(
                    item.VoucherNumber,
                    item.OnDate,
                    item.VoucherType,
                    item.PartyName,
                    item.Particulars,
                    item.PaymentMode,
                    item.Amount,
                    item.Remarks));
            }),
        ["petty-cash"] = new(
            "PettyCash",
            ["Date", "StoreId", "Opening", "Sales", "Receipts", "Expenses", "Payments", "BankDeposit", "CashInHand"],
            async (db, cancellationToken) =>
            {
                var rows = await db.PettyCashSheets.AsNoTracking()
                    .OrderByDescending(item => item.OnDate)
                    .ToListAsync(cancellationToken);

                return rows.Select(item => Row(
                    item.OnDate,
                    item.StoreId,
                    item.OpeningBalance,
                    item.Sales,
                    item.Receipts,
                    item.Expenses,
                    item.Payments,
                    item.BankDeposit,
                    item.CashInHand));
            }),
        ["hr"] = new(
            "HR",
            ["Employee", "Mobile", "Email", "JoiningDate", "LeavingDate", "Working", "Category"],
            async (db, cancellationToken) =>
            {
                var rows = await db.Employees.AsNoTracking()
                    .OrderBy(item => item.FirstName)
                    .ThenBy(item => item.LastName)
                    .ToListAsync(cancellationToken);

                return rows.Select(item => Row(
                    item.StaffName,
                    item.Mobile,
                    item.Email,
                    item.JoiningDate,
                    item.LeavingDate,
                    item.Working,
                    item.Category));
            }),
        ["payroll"] = new(
            "Payroll",
            ["Type", "Employee", "Mobile", "Month", "FromDate", "ToDate", "Basic", "HRA", "SpecialAllowance", "Conveyance", "Incentives", "ProvidentFund", "Gratuity", "ProfessionalTax", "Deductions", "GrossSalary", "TotalDeductions", "NetSalary", "SalaryComponent", "VoucherNumber", "PaymentDate", "PaymentMode", "Amount", "Remarks"],
            async (db, cancellationToken) =>
            {
                var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
                var structures = await db.SalaryStructures.AsNoTracking()
                    .OrderBy(item => item.EmployeeId)
                    .ThenByDescending(item => item.FromDate)
                    .ToListAsync(cancellationToken);
                var payments = await db.SalaryPayments.AsNoTracking()
                    .OrderByDescending(item => item.OnDate)
                    .ToListAsync(cancellationToken);
                var rows = structures.Select(item =>
                {
                    employees.TryGetValue(item.EmployeeId, out var employee);
                    return Row(
                        "Structure",
                        employee?.StaffName ?? item.EmployeeId.ToString(),
                        employee?.Mobile ?? "",
                        "",
                        item.FromDate,
                        item.ToDate,
                        item.BasicSalary,
                        item.HRA,
                        item.SpecialAllowance,
                        item.ConveyanceAllowance,
                        item.Incentives,
                        item.ProvidentFund,
                        item.Gratuity,
                        item.ProfessionalTax,
                        item.Deductions,
                        item.BasicSalary + item.HRA + item.SpecialAllowance + item.ConveyanceAllowance + item.Incentives,
                        item.ProvidentFund + item.Gratuity + item.ProfessionalTax + item.Deductions,
                        item.NetSalary,
                        "",
                        "",
                        "",
                        "",
                        "",
                        "");
                });

                return rows.Concat(payments.Select(item =>
                {
                    employees.TryGetValue(item.EmployeeId, out var employee);
                    return Row(
                        "Payment",
                        employee?.StaffName ?? item.EmployeeId.ToString(),
                        employee?.Mobile ?? "",
                        item.SalaryMonth,
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        item.GrossSalary,
                        item.TotalDeductions,
                        item.NetSalary,
                        item.SalaryComponent,
                        item.VoucherNumber,
                        item.OnDate,
                        item.PaymentMode,
                        item.Amount,
                        item.Remarks);
                }));
            }),
        ["access"] = new(
            "Access",
            ["Name", "UserName", "Email", "Password", "Role", "UserType", "IsActive", "AppOperation", "CompanyCode", "StoreGroupCode", "StoreCode"],
            async (db, cancellationToken) =>
            {
                var rows = await db.Users.AsNoTracking()
                    .OrderBy(item => item.UserName)
                    .ToListAsync(cancellationToken);
                var companies = await db.Companies.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
                var groups = await db.StoreGroups.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
                var stores = await db.Stores.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);

                return rows.Select(item => Row(
                    item.Name,
                    item.UserName,
                    item.Email,
                    "",
                    item.Role,
                    item.UserType,
                    item.IsActive,
                    item.AppOperation,
                    item.CompanyId.HasValue && companies.TryGetValue(item.CompanyId.Value, out var company) ? company.Code : "",
                    item.StoreGroupId.HasValue && groups.TryGetValue(item.StoreGroupId.Value, out var group) ? group.GroupCode : "",
                    item.StoreId.HasValue && stores.TryGetValue(item.StoreId.Value, out var store) ? store.StoreCode : ""));
            })
    };

    public static RouteGroupBuilder MapImportExportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/import-export")
            .WithTags("ImportExport")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/modules", () => Results.Ok(Definitions.Select(item => new
        {
            key = item.Key,
            name = item.Value.Name,
            columns = item.Value.Headers.Length,
            importSupported = item.Key is "setup" or "inventory" or "billing" or "purchase" or "hr" or "payroll" or "vouchers" or "petty-cash" or "access"
        })));
        group.MapGet("/export/{module}", ExportModuleAsync);
        group.MapGet("/template/{module}", TemplateAsync);
        group.MapPost("/import/{module}", ImportModuleAsync).DisableAntiforgery();

        return group;
    }

    private static async Task<IResult> ExportModuleAsync(string module, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (!Definitions.TryGetValue(module, out var definition))
        {
            return Results.NotFound(new { message = "Export module not found." });
        }

        var rows = await definition.Export(db, cancellationToken);
        return CsvFile($"{definition.Name}-{DateTime.Now:yyyyMMdd-HHmm}.csv", definition.Headers, rows);
    }

    private static IResult TemplateAsync(string module)
    {
        if (!Definitions.TryGetValue(module, out var definition))
        {
            return Results.NotFound(new { message = "Import template not found." });
        }

        return CsvFile($"{definition.Name}-ImportTemplate.csv", definition.Headers, []);
    }

    private static async Task<IResult> ImportModuleAsync(
        string module,
        IFormFile file,
        bool commit,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (!Definitions.TryGetValue(module, out var definition))
        {
            return Results.NotFound(new { message = "Import module not found." });
        }

        if (file.Length == 0)
        {
            return Results.BadRequest(new { message = "Upload a CSV file." });
        }

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = ParseCsv(await reader.ReadToEndAsync(cancellationToken));
        var result = new ImportResult(module, commit);

        if (rows.Count == 0)
        {
            result.Errors.Add(new ImportRowError(1, "File", "CSV file is empty."));
            return Results.Ok(result);
        }

        var headers = rows[0].Select(header => header.Trim()).ToArray();
        var missingHeaders = definition.Headers
            .Where(header => !headers.Any(value => string.Equals(value, header, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingHeaders.Count > 0)
        {
            result.Errors.Add(new ImportRowError(1, "Header", $"Missing columns: {string.Join(", ", missingHeaders)}."));
            return Results.Ok(result);
        }

        var dataRows = rows.Skip(1)
            .Select((values, index) => new CsvDataRow(index + 2, headers, values))
            .Where(row => !row.IsBlank)
            .ToList();

        result.RowsRead = dataRows.Count;

        switch (module.ToLowerInvariant())
        {
            case "setup":
                await ImportSetupAsync(db, dataRows, commit, result, cancellationToken);
                break;
            case "inventory":
                await ImportInventoryAsync(db, dataRows, commit, result, cancellationToken);
                break;
            case "hr":
                await ImportHrAsync(db, dataRows, commit, result, cancellationToken);
                break;
            case "vouchers":
                await ImportVouchersAsync(db, dataRows, commit, result, cancellationToken);
                break;
            case "petty-cash":
                await ImportPettyCashAsync(db, dataRows, commit, result, cancellationToken);
                break;
            case "access":
                await ImportAccessAsync(db, dataRows, commit, result, cancellationToken);
                break;
            case "purchase":
                await ImportPurchaseAsync(db, dataRows, commit, result, accounting, cancellationToken);
                break;
            case "billing":
                await ImportBillingAsync(db, dataRows, commit, result, accounting, cancellationToken);
                break;
            case "payroll":
                await ImportPayrollAsync(db, dataRows, commit, result, accounting, cancellationToken);
                break;
            default:
                result.Errors.Add(new ImportRowError(1, "Module", $"{definition.Name} import write is not enabled yet. Download/export is available."));
                break;
        }

        result.InvalidRows = result.Errors.Select(error => error.Line).Distinct().Count(line => line > 1);
        result.ValidRows = Math.Max(0, result.RowsRead - result.InvalidRows);

        if (commit && result.Errors.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (commit)
        {
            result.Created = 0;
            result.Updated = 0;
        }

        return Results.Ok(result);
    }

    private static async Task ImportSetupAsync(
        GarmetixDbContext db,
        IReadOnlyList<CsvDataRow> rows,
        bool commit,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var companies = await db.Companies.OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var groups = await db.StoreGroups.OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var stores = await db.Stores.OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);

        var companiesByCode = new Dictionary<string, Company>(StringComparer.OrdinalIgnoreCase);
        var companiesByName = new Dictionary<string, Company>(StringComparer.OrdinalIgnoreCase);
        var groupsByKey = new Dictionary<string, StoreGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var company in companies)
        {
            AddLookup(companiesByCode, company.Code, company);
            AddLookup(companiesByName, company.Name, company);
        }

        foreach (var group in groups)
        {
            AddLookup(groupsByKey, GroupKey(group.CompanyId, group.GroupCode), group);
            AddLookup(groupsByKey, GroupKey(group.CompanyId, group.Name), group);
        }

        foreach (var row in rows)
        {
            var type = row.Required("Type", result).Replace(" ", "", StringComparison.OrdinalIgnoreCase);
            var name = row.Required("Name", result);
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var code = row["Code"];
            if (string.IsNullOrWhiteSpace(code))
            {
                code = MakeCode(name);
                result.Warnings.Add($"Line {row.Line}: Code was blank, generated '{code}'.");
            }

            switch (type.ToLowerInvariant())
            {
                case "company":
                    ImportCompanyRow(db, row, name, code, companiesByCode, companiesByName, commit, result);
                    break;
                case "storegroup":
                    ImportStoreGroupRow(db, row, name, code, companiesByCode, companiesByName, groupsByKey, commit, result);
                    break;
                case "store":
                    ImportStoreRow(db, row, name, code, companiesByCode, companiesByName, groupsByKey, stores, commit, result);
                    break;
                default:
                    result.Errors.Add(new ImportRowError(row.Line, "Type", "Type must be Company, StoreGroup, or Store."));
                    break;
            }
        }
    }

    private static async Task ImportAccessAsync(
        GarmetixDbContext db,
        IReadOnlyList<CsvDataRow> rows,
        bool commit,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var users = await db.Users.OrderBy(item => item.UserName).ToListAsync(cancellationToken);
        var companies = await db.Companies.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var groups = await db.StoreGroups.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var stores = await db.Stores.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var userByUserName = users
            .Where(item => !string.IsNullOrWhiteSpace(item.UserName))
            .GroupBy(item => item.UserName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.First(), StringComparer.OrdinalIgnoreCase);
        var userByEmail = users
            .Where(item => !string.IsNullOrWhiteSpace(item.Email))
            .GroupBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var name = row.Required("Name", result);
            var userName = row.Required("UserName", result);
            var email = row.Required("Email", result);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            var byUserName = userByUserName.GetValueOrDefault(userName);
            var byEmail = userByEmail.GetValueOrDefault(email);
            if (byUserName is not null && byEmail is not null && byUserName.Id != byEmail.Id)
            {
                result.Errors.Add(new ImportRowError(row.Line, "Email", "Username and email belong to different existing users."));
                continue;
            }

            var user = byUserName ?? byEmail;
            var created = user is null;
            var password = row["Password"];
            if (created && string.IsNullOrWhiteSpace(password))
            {
                result.Errors.Add(new ImportRowError(row.Line, "Password", "Password is required for new users."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(password) && password.Length < 6)
            {
                result.Errors.Add(new ImportRowError(row.Line, "Password", "Password must be at least 6 characters."));
                continue;
            }

            var role = row.Enum("Role", LoginRole.Member, result);
            var userType = row.Enum("UserType", UserType.StoreManager, result);
            var admin = role == LoginRole.Admin;
            var isActive = row.Bool("IsActive", true);
            var appOperation = row.Enum("AppOperation", AppOperation.All, result);
            var scope = ResolveUserScope(row, companies, groups, stores, result);

            if (user is not null
                && user.IsActive
                && (user.Admin || user.Role == LoginRole.Admin)
                && (!admin || !isActive))
            {
                var adminCount = users.Count(item => item.IsActive && (item.Admin || item.Role == LoginRole.Admin));
                if (adminCount <= 1)
                {
                    result.Errors.Add(new ImportRowError(row.Line, "Role", "Cannot remove or deactivate the last active admin user."));
                    continue;
                }
            }

            if (!commit || result.HasLineError(row.Line))
            {
                continue;
            }

            user ??= new AppUser();
            user.Name = name.Trim();
            user.UserName = userName.Trim();
            user.Email = email.Trim();
            user.Role = role;
            user.UserType = userType;
            user.Admin = admin;
            user.IsActive = isActive;
            user.AppOperation = appOperation;
            user.CompanyId = scope.CompanyId;
            user.StoreGroupId = scope.StoreGroupId;
            user.StoreId = scope.StoreId;

            if (!string.IsNullOrWhiteSpace(password))
            {
                user.Password = PasswordHasher.Hash(password);
            }

            if (created)
            {
                db.Users.Add(user);
                userByUserName[user.UserName] = user;
                userByEmail[user.Email] = user;
                users.Add(user);
                result.Created++;
            }
            else
            {
                result.Updated++;
            }
        }
    }

    private static async Task ImportPurchaseAsync(
        GarmetixDbContext db,
        IReadOnlyList<CsvDataRow> rows,
        bool commit,
        ImportResult result,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var companies = await db.Companies.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var groups = await db.StoreGroups.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var stores = await db.Stores.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var existingInvoices = await db.PurchaseInvoices.AsNoTracking()
            .Select(item => new { item.CompanyId, item.InvoiceNumber })
            .ToListAsync(cancellationToken);
        var bankAccounts = await db.BankAccounts.AsNoTracking().ToListAsync(cancellationToken);
        var drafts = new List<PurchaseImportLine>();

        foreach (var row in rows)
        {
            var invoiceNumber = row.Required("InvoiceNumber", result);
            var vendorName = row.Required("Vendor", result);
            var productName = row.Required("Product", result);
            var barcode = row.Required("Barcode", result);
            if (string.IsNullOrWhiteSpace(invoiceNumber) ||
                string.IsNullOrWhiteSpace(vendorName) ||
                string.IsNullOrWhiteSpace(productName) ||
                string.IsNullOrWhiteSpace(barcode))
            {
                continue;
            }

            var scope = ResolveRequiredStoreScope(row, companies, groups, stores, result);
            var quantity = row.Decimal("Quantity", result);
            var costPrice = row.Decimal("CostPrice", result);
            var mrp = row.Decimal("MRP", result);
            var discount = row.Decimal("Discount", result);
            var taxRate = row.Decimal("TaxRate", result);
            var paid = row.Decimal("Paid", result);
            var freight = row.Decimal("FrightAmount", result);
            var paymentMode = row.Enum("PaymentMode", PaymentMode.Cash, result);
            var bankAccount = ResolveBankAccount(row, scope.CompanyId, bankAccounts, result);

            if (quantity <= 0)
            {
                result.Errors.Add(new ImportRowError(row.Line, "Quantity", "Quantity must be greater than zero."));
            }

            if (costPrice < 0 || mrp < 0 || discount < 0 || paid < 0 || freight < 0)
            {
                result.Errors.Add(new ImportRowError(row.Line, "Amount", "Amounts cannot be negative."));
            }

            if (paid > 0 && paymentMode != PaymentMode.Cash && bankAccount is null)
            {
                result.Errors.Add(new ImportRowError(row.Line, "BankAccountNumber", "Bank account is required for non-cash purchase payment."));
            }

            if (existingInvoices.Any(item =>
                item.CompanyId == scope.CompanyId &&
                item.InvoiceNumber.Equals(invoiceNumber, StringComparison.OrdinalIgnoreCase)))
            {
                result.Errors.Add(new ImportRowError(row.Line, "InvoiceNumber", "Purchase invoice already exists. Purchase import is create-only."));
            }

            drafts.Add(new PurchaseImportLine(
                row.Line,
                scope.CompanyId,
                scope.StoreGroupId,
                scope.StoreId,
                invoiceNumber.Trim(),
                RequiredOrDefault(row["InwardNumber"], $"INW-{invoiceNumber.Trim()}"),
                row.Date("Date", DateTime.Today, result),
                vendorName.Trim(),
                row["VendorMobile"],
                row["GSTIN"],
                productName.Trim(),
                barcode.Trim(),
                quantity,
                costPrice,
                mrp,
                discount,
                taxRate,
                paid,
                paymentMode,
                bankAccount?.Id,
                freight));
        }

        foreach (var group in drafts.GroupBy(item => new { item.CompanyId, item.InvoiceNumber }))
        {
            var first = group.First();
            if (group.Select(item => item.VendorName).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            {
                result.Errors.Add(new ImportRowError(first.Line, "Vendor", "All rows for one purchase invoice must use the same vendor."));
            }

            if (group.Select(item => item.StoreId).Distinct().Count() > 1)
            {
                result.Errors.Add(new ImportRowError(first.Line, "StoreCode", "All rows for one purchase invoice must use the same store."));
            }
        }

        if (!commit || result.Errors.Count > 0)
        {
            if (!commit)
            {
                result.Warnings.Add("Purchase import will create invoices, vendors/products if missing, stock entries, ledger postings, and bank transactions when committed.");
            }

            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var categoryCache = new Dictionary<Guid, InventoryProductCategory>();
        var subCategoryCache = new Dictionary<Guid, InventoryProductSubCategory>();
        var taxCache = await db.Taxes.ToListAsync(cancellationToken);

        foreach (var group in drafts.GroupBy(item => new { item.CompanyId, item.InvoiceNumber }))
        {
            var first = group.First();
            var vendor = await GetOrCreateImportVendorAsync(db, first, cancellationToken);
            var invoiceId = Guid.NewGuid();
            var invoiceItems = new List<PurchaseInvoiceItem>();
            decimal grossMrp = 0;
            decimal discountAmount = 0;
            decimal taxableAmount = 0;
            decimal taxAmount = 0;
            decimal totalQuantity = 0;

            foreach (var line in group)
            {
                var category = await GetOrCreateGeneralCategoryAsync(db, categoryCache, line.CompanyId, cancellationToken);
                var subCategory = await GetOrCreateGeneralSubCategoryAsync(db, subCategoryCache, line.CompanyId, cancellationToken);
                var tax = await GetOrCreateImportTaxAsync(db, taxCache, line.TaxRate, cancellationToken);
                var product = await GetOrCreateImportProductAsync(db, line, category.Id, subCategory.Id, tax, cancellationToken);

                var lineMrp = line.Mrp * line.Quantity;
                var lineCost = line.CostPrice * line.Quantity;
                var lineNet = Math.Max(lineCost - line.Discount, 0);
                var taxable = Math.Round(lineNet / (1 + (tax.CompositeRate / 100)), 2);
                var taxValue = Math.Round(taxable * (tax.CompositeRate / 100), 2);
                var lineAmount = taxable + taxValue;

                invoiceItems.Add(new PurchaseInvoiceItem
                {
                    InvoiceId = invoiceId,
                    ProductId = product.Id,
                    Barcode = line.Barcode,
                    MRP = line.Mrp,
                    DiscountAmount = line.Discount,
                    BasePrice = taxable,
                    TaxPercentage = tax.CompositeRate,
                    TaxAmount = taxValue,
                    Amount = lineAmount,
                    TaxType = tax.TaxType,
                    TaxId = tax.Id,
                    BilledQuantity = line.Quantity,
                    CompanyId = line.CompanyId
                });

                var stock = await db.Stocks.FirstOrDefaultAsync(item =>
                    item.ProductId == product.Id &&
                    item.Barcode == line.Barcode &&
                    item.StoreId == line.StoreId &&
                    !item.IsOFB,
                    cancellationToken);

                if (stock is null)
                {
                    stock = new Stock
                    {
                        ProductId = product.Id,
                        Barcode = line.Barcode,
                        Unit = Unit.Pcs,
                        CompanyId = line.CompanyId,
                        StoreGroupId = line.StoreGroupId,
                        StoreId = line.StoreId,
                        TaxId = tax.Id,
                        IsOFB = false
                    };
                    db.Stocks.Add(stock);
                }

                stock.PurchaseQty += line.Quantity;
                stock.CostPrice = line.CostPrice;
                stock.MRP = line.Mrp;
                stock.TaxRate = tax.CompositeRate;
                stock.TaxType = tax.TaxType;
                stock.TaxId = tax.Id;

                product.MRP = line.Mrp;
                product.TaxRate = tax.CompositeRate;
                product.TaxType = tax.TaxType;

                grossMrp += lineMrp;
                discountAmount += line.Discount;
                taxableAmount += taxable;
                taxAmount += taxValue;
                totalQuantity += line.Quantity;
            }

            var freightAmount = group.Max(item => item.FrightAmount);
            var paidAmount = group.Max(item => item.PaidAmount);
            var netAmount = taxableAmount + taxAmount + freightAmount;
            var billAmount = Math.Round(netAmount, 0);
            paidAmount = Math.Min(Math.Max(paidAmount, 0), billAmount);

            var invoice = new PurchaseInvoice
            {
                Id = invoiceId,
                InvoiceNumber = first.InvoiceNumber,
                InwardNumber = first.InwardNumber,
                InwardDate = first.OnDate,
                OnDate = first.OnDate,
                InvoiceType = InvoiceType.Regular,
                InvoiceStatus = paidAmount <= 0
                    ? InvoiceStatus.Pending
                    : paidAmount >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid,
                MRP = grossMrp,
                BasePrice = taxableAmount,
                DiscountAmount = discountAmount,
                TaxAmount = taxAmount,
                NetAmount = taxableAmount + taxAmount,
                RoundOff = billAmount - netAmount,
                BillAmount = billAmount,
                Quantity = totalQuantity,
                ItemCount = invoiceItems.Count,
                PaymentMode = paidAmount > 0 ? first.PaymentMode : null,
                VendorId = vendor.Id,
                VendorName = vendor.Name,
                VendorGSTIN = vendor.GSTIN,
                FrightAmount = freightAmount,
                DueDate = first.OnDate.AddDays(45),
                CompanyId = first.CompanyId
            };

            db.PurchaseInvoices.Add(invoice);
            db.PurchaseInvoiceItems.AddRange(invoiceItems);
            vendor.BillCount += 1;
            vendor.BillAmount += billAmount;
            vendor.Paid += paidAmount;
            await accounting.PostPurchaseInvoiceAsync(invoice, vendor, paidAmount, first.StoreGroupId, first.StoreId, first.BankAccountId, cancellationToken);
            result.Created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ImportBillingAsync(
        GarmetixDbContext db,
        IReadOnlyList<CsvDataRow> rows,
        bool commit,
        ImportResult result,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var companies = await db.Companies.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var groups = await db.StoreGroups.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var stores = await db.Stores.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var existingInvoices = await db.SalesInvoices.AsNoTracking()
            .Select(item => new { item.CompanyId, item.StoreId, item.InvoiceNumber })
            .ToListAsync(cancellationToken);
        var bankAccounts = await db.BankAccounts.AsNoTracking().ToListAsync(cancellationToken);
        var drafts = new List<BillingImportLine>();

        foreach (var row in rows)
        {
            var invoiceNumber = row.Required("InvoiceNumber", result);
            var barcode = row.Required("Barcode", result);
            if (string.IsNullOrWhiteSpace(invoiceNumber) || string.IsNullOrWhiteSpace(barcode))
            {
                continue;
            }

            var scope = ResolveRequiredStoreScope(row, companies, groups, stores, result);
            var quantity = row.Decimal("Quantity", result);
            var mrp = row.Decimal("MRP", result);
            var discount = row.Decimal("Discount", result);
            var paid = row.Decimal("Paid", result);
            var billDiscount = row.Decimal("BillDiscount", result);
            var paymentMode = row.Enum("PaymentMode", PaymentMode.Cash, result);
            var bankAccount = ResolveBankAccount(row, scope.CompanyId, bankAccounts, result);

            if (quantity <= 0)
            {
                result.Errors.Add(new ImportRowError(row.Line, "Quantity", "Quantity must be greater than zero."));
            }

            if (mrp < 0 || discount < 0 || paid < 0 || billDiscount < 0)
            {
                result.Errors.Add(new ImportRowError(row.Line, "Amount", "Amounts cannot be negative."));
            }

            if (paid > 0 && paymentMode != PaymentMode.Cash && bankAccount is null)
            {
                result.Errors.Add(new ImportRowError(row.Line, "BankAccountNumber", "Bank account is required for non-cash billing payment."));
            }

            if (existingInvoices.Any(item =>
                item.CompanyId == scope.CompanyId &&
                item.StoreId == scope.StoreId &&
                item.InvoiceNumber.Equals(invoiceNumber, StringComparison.OrdinalIgnoreCase)))
            {
                result.Errors.Add(new ImportRowError(row.Line, "InvoiceNumber", "Sales invoice already exists. Billing import is create-only."));
            }

            drafts.Add(new BillingImportLine(
                row.Line,
                scope.CompanyId,
                scope.StoreGroupId,
                scope.StoreId,
                invoiceNumber.Trim(),
                row.Date("Date", DateTime.Today, result),
                RequiredOrDefault(row["Customer"], "Walk-in Customer"),
                RequiredOrDefault(row["Mobile"], "WALKIN"),
                row["Product"],
                barcode.Trim(),
                quantity,
                mrp,
                discount,
                paid,
                paymentMode,
                bankAccount?.Id,
                billDiscount));
        }

        var requiredStock = drafts
            .GroupBy(item => new { item.StoreId, item.Barcode })
            .ToDictionary(item => item.Key, item => item.Sum(line => line.Quantity));
        var requiredStoreIds = requiredStock.Keys.Select(key => key.StoreId).Distinct().ToList();
        var stockRows = await db.Stocks
            .Include(item => item.Product)
            .Where(item => !item.IsOFB && requiredStoreIds.Contains(item.StoreId))
            .ToListAsync(cancellationToken);

        foreach (var group in drafts.GroupBy(item => new { item.CompanyId, item.StoreId, item.InvoiceNumber }))
        {
            var first = group.First();
            if (group.Select(item => item.CustomerMobile).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            {
                result.Errors.Add(new ImportRowError(first.Line, "Mobile", "All rows for one sales invoice must use the same customer mobile."));
            }

            if (group.Select(item => item.PaymentMode).Distinct().Count() > 1)
            {
                result.Errors.Add(new ImportRowError(first.Line, "PaymentMode", "All rows for one sales invoice must use the same payment mode."));
            }
        }

        foreach (var stockNeed in requiredStock)
        {
            var stock = stockRows.FirstOrDefault(item =>
                item.StoreId == stockNeed.Key.StoreId &&
                item.Barcode.Equals(stockNeed.Key.Barcode, StringComparison.OrdinalIgnoreCase));

            if (stock is null)
            {
                var line = drafts.First(item => item.StoreId == stockNeed.Key.StoreId && item.Barcode.Equals(stockNeed.Key.Barcode, StringComparison.OrdinalIgnoreCase));
                result.Errors.Add(new ImportRowError(line.Line, "Barcode", $"Stock not found for barcode {line.Barcode}."));
                continue;
            }

            if (stock.CurrentStock < stockNeed.Value)
            {
                var line = drafts.First(item => item.StoreId == stockNeed.Key.StoreId && item.Barcode.Equals(stockNeed.Key.Barcode, StringComparison.OrdinalIgnoreCase));
                result.Errors.Add(new ImportRowError(line.Line, "Quantity", $"Insufficient stock for {line.Barcode}. Available: {stock.CurrentStock}."));
            }
        }

        if (!commit || result.Errors.Count > 0)
        {
            if (!commit)
            {
                result.Warnings.Add("Billing import will create sales invoices, customers if missing, reduce stock, post ledgers, and create bank transactions when committed.");
            }

            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var group in drafts.GroupBy(item => new { item.CompanyId, item.StoreId, item.InvoiceNumber }))
        {
            var first = group.First();
            var customer = await GetOrCreateImportCustomerAsync(db, first, cancellationToken);
            var invoiceId = Guid.NewGuid();
            var invoiceItems = new List<InvoiceItem>();
            decimal grossMrp = 0;
            decimal itemDiscount = 0;
            decimal taxableAmount = 0;
            decimal taxAmount = 0;
            decimal totalQuantity = 0;

            foreach (var line in group)
            {
                var stock = stockRows.First(item =>
                    item.StoreId == line.StoreId &&
                    item.Barcode.Equals(line.Barcode, StringComparison.OrdinalIgnoreCase));
                var product = stock.Product ?? await db.Products.FirstAsync(item => item.Id == stock.ProductId, cancellationToken);
                var lineMrp = line.Mrp * line.Quantity;
                var lineDiscount = line.Discount * line.Quantity;
                var taxable = Math.Round((lineMrp - lineDiscount) / (1 + (stock.TaxRate / 100)), 2);
                var tax = Math.Round(taxable * (stock.TaxRate / 100), 2);
                var lineAmount = taxable + tax;

                invoiceItems.Add(new InvoiceItem
                {
                    InvoiceId = invoiceId,
                    ProductId = product.Id,
                    Barcode = line.Barcode,
                    MRP = line.Mrp,
                    DiscountAmount = line.Discount,
                    BasePrice = taxable,
                    TaxPercentage = stock.TaxRate,
                    TaxAmount = tax,
                    Amount = lineAmount,
                    TaxType = stock.TaxType,
                    TaxId = stock.TaxId,
                    BilledQuantity = line.Quantity,
                    CompanyId = line.CompanyId
                });

                stock.SoldQty += line.Quantity;
                stock.SoldValue += lineAmount;
                grossMrp += lineMrp;
                itemDiscount += lineDiscount;
                taxableAmount += taxable;
                taxAmount += tax;
                totalQuantity += line.Quantity;
            }

            var billDiscount = group.Max(item => item.BillDiscount);
            var totalDiscount = itemDiscount + billDiscount;
            var billAmount = Math.Round(grossMrp - totalDiscount, 0);
            var paidAmount = Math.Min(Math.Max(group.Max(item => item.PaidAmount), 0), billAmount);

            var invoice = new Invoice
            {
                Id = invoiceId,
                InvoiceNumber = first.InvoiceNumber,
                OnDate = first.OnDate,
                InvoiceType = InvoiceType.Regular,
                InvoiceStatus = paidAmount <= 0
                    ? InvoiceStatus.Pending
                    : paidAmount >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid,
                MRP = grossMrp,
                BasePrice = taxableAmount,
                DiscountAmount = totalDiscount,
                TaxAmount = taxAmount,
                NetAmount = taxableAmount,
                RoundOff = billAmount - (taxableAmount + taxAmount),
                BillAmount = billAmount,
                Quantity = totalQuantity,
                ItemCount = invoiceItems.Count,
                PaymentMode = paidAmount > 0 ? first.PaymentMode : null,
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CustomerMobileNumber = customer.MobileNumber,
                CreditSale = paidAmount < billAmount,
                PaidAmount = paidAmount,
                BillDiscountAmount = billDiscount,
                StoreId = first.StoreId,
                CompanyId = first.CompanyId
            };

            db.SalesInvoices.Add(invoice);
            db.InvoiceItems.AddRange(invoiceItems);

            if (paidAmount > 0)
            {
                db.InvoicePayments.Add(new InvoicePayment
                {
                    InvoiceId = invoice.Id,
                    OnDate = first.OnDate,
                    Amount = paidAmount,
                    PaymentMode = first.PaymentMode,
                    StoreId = first.StoreId,
                    CompanyId = first.CompanyId
                });
            }

            customer.BillCount += 1;
            customer.Amount += billAmount;
            await accounting.PostSalesInvoiceAsync(invoice, customer, first.StoreGroupId, first.BankAccountId, cancellationToken);
            result.Created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ImportPayrollAsync(
        GarmetixDbContext db,
        IReadOnlyList<CsvDataRow> rows,
        bool commit,
        ImportResult result,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var employees = await db.Employees
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .ToListAsync(cancellationToken);
        var structures = await db.SalaryStructures.ToListAsync(cancellationToken);
        var payments = await db.SalaryPayments.ToListAsync(cancellationToken);
        var payslips = await db.SalaryPaySlips.ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            var type = row.Required("Type", result).Replace(" ", "", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            var employee = ResolvePayrollEmployee(row, employees, result);
            if (employee is null)
            {
                continue;
            }

            switch (type.ToLowerInvariant())
            {
                case "structure":
                case "salarystructure":
                    ImportSalaryStructureRow(db, row, employee, structures, commit, result);
                    break;
                case "payment":
                case "salarypayment":
                case "advance":
                    await ImportSalaryPaymentRow(db, row, employee, structures, payments, payslips, commit, result, accounting, cancellationToken);
                    break;
                default:
                    result.Errors.Add(new ImportRowError(row.Line, "Type", "Type must be Structure or Payment."));
                    break;
            }
        }
    }

    private static void ImportSalaryStructureRow(
        GarmetixDbContext db,
        CsvDataRow row,
        Employee employee,
        List<SalaryStructure> structures,
        bool commit,
        ImportResult result)
    {
        var fromDate = row.Date("FromDate", DateTime.Today, result);
        var toDate = row.OptionalDate("ToDate", result);
        var basic = row.Decimal("Basic", result);
        var hra = row.Decimal("HRA", result);
        var specialAllowance = row.Decimal("SpecialAllowance", result);
        var conveyance = row.Decimal("Conveyance", result);
        var incentives = row.Decimal("Incentives", result);
        var providentFund = row.Decimal("ProvidentFund", result);
        var gratuity = row.Decimal("Gratuity", result);
        var professionalTax = row.Decimal("ProfessionalTax", result);
        var deductions = row.Decimal("Deductions", result);

        if (toDate.HasValue && toDate.Value.Date < fromDate.Date)
        {
            result.Errors.Add(new ImportRowError(row.Line, "ToDate", "ToDate cannot be earlier than FromDate."));
        }

        if (new[] { basic, hra, specialAllowance, conveyance, incentives, providentFund, gratuity, professionalTax, deductions }.Any(item => item < 0))
        {
            result.Errors.Add(new ImportRowError(row.Line, "Amount", "Salary structure amounts cannot be negative."));
        }

        if (!commit || result.HasLineError(row.Line))
        {
            return;
        }

        var structure = structures.FirstOrDefault(item => item.EmployeeId == employee.Id && item.FromDate.Date == fromDate.Date);
        var created = structure is null;
        structure ??= new SalaryStructure
        {
            EmployeeId = employee.Id,
            FromDate = fromDate,
            CompanyId = employee.CompanyId
        };

        structure.EmployeeId = employee.Id;
        structure.CompanyId = employee.CompanyId;
        structure.FromDate = fromDate;
        structure.ToDate = toDate;
        structure.BasicSalary = basic;
        structure.HRA = hra;
        structure.SpecialAllowance = specialAllowance;
        structure.ConveyanceAllowance = conveyance;
        structure.Incentives = incentives;
        structure.ProvidentFund = providentFund;
        structure.Gratuity = gratuity;
        structure.ProfessionalTax = professionalTax;
        structure.Deductions = deductions;

        if (created)
        {
            db.SalaryStructures.Add(structure);
            structures.Add(structure);
            result.Created++;
        }
        else
        {
            result.Updated++;
        }
    }

    private static async Task ImportSalaryPaymentRow(
        GarmetixDbContext db,
        CsvDataRow row,
        Employee employee,
        List<SalaryStructure> structures,
        List<SalaryPayment> payments,
        List<SalaryPaySlip> payslips,
        bool commit,
        ImportResult result,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var voucherNumber = row.Required("VoucherNumber", result);
        var onDate = row.Date("PaymentDate", DateTime.Today, result);
        var salaryMonth = row.SalaryMonth("Month", onDate, result);
        var component = row.Enum("SalaryComponent", SalaryComponent.NetSalary, result);
        var paymentMode = row.Enum("PaymentMode", PaymentMode.Cash, result);
        var amount = row.Decimal("Amount", result);
        var grossSalary = row.Decimal("GrossSalary", result);
        var totalDeductions = row.Decimal("TotalDeductions", result);
        var netSalary = row.Decimal("NetSalary", result);

        if (string.IsNullOrWhiteSpace(voucherNumber))
        {
            return;
        }

        if (amount <= 0)
        {
            result.Errors.Add(new ImportRowError(row.Line, "Amount", "Payment amount must be greater than zero."));
        }

        if (!commit || result.HasLineError(row.Line))
        {
            return;
        }

        var monthStart = new DateTime(salaryMonth / 100, salaryMonth % 100, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var structure = structures
            .Where(item =>
                item.EmployeeId == employee.Id &&
                item.FromDate.Date <= monthEnd &&
                (item.ToDate == null || item.ToDate.Value.Date >= monthStart))
            .OrderByDescending(item => item.FromDate)
            .FirstOrDefault();
        var payslip = payslips.FirstOrDefault(item => item.EmployeeId == employee.Id && item.PayPeriodStart.Date == monthStart);

        if (grossSalary <= 0 && structure is not null)
        {
            grossSalary = structure.BasicSalary + structure.HRA + structure.SpecialAllowance + structure.ConveyanceAllowance + structure.Incentives;
        }

        if (totalDeductions <= 0 && structure is not null)
        {
            totalDeductions = structure.ProvidentFund + structure.Gratuity + structure.ProfessionalTax + structure.Deductions;
        }

        if (netSalary <= 0)
        {
            netSalary = payslip?.NetSalary ?? structure?.NetSalary ?? amount;
        }

        var payment = payments.FirstOrDefault(item =>
            item.EmployeeId == employee.Id &&
            item.VoucherNumber.Equals(voucherNumber, StringComparison.OrdinalIgnoreCase));
        var created = payment is null;
        payment ??= new SalaryPayment
        {
            EmployeeId = employee.Id,
            VoucherNumber = voucherNumber,
            CompanyId = employee.CompanyId,
            StoreGroupId = employee.StoreGroupId,
            StoreId = employee.StoreId
        };

        payment.EmployeeId = employee.Id;
        payment.CompanyId = employee.CompanyId;
        payment.StoreGroupId = employee.StoreGroupId;
        payment.StoreId = employee.StoreId;
        payment.VoucherNumber = voucherNumber.Trim();
        payment.SalaryMonth = salaryMonth;
        payment.OnDate = onDate;
        payment.SalaryComponent = component;
        payment.GrossSalary = grossSalary;
        payment.TotalDeductions = totalDeductions;
        payment.NetSalary = netSalary;
        payment.Amount = amount;
        payment.PaymentMode = paymentMode;
        payment.Remarks = row["Remarks"];
        payment.SalaryPaySlipId = payslip?.Id;

        if (created)
        {
            db.SalaryPayments.Add(payment);
            payments.Add(payment);
            result.Created++;
        }
        else
        {
            result.Updated++;
        }

        await accounting.PostSalaryPaymentAsync(payment, cancellationToken);
    }

    private static void ImportCompanyRow(
        GarmetixDbContext db,
        CsvDataRow row,
        string name,
        string code,
        Dictionary<string, Company> companiesByCode,
        Dictionary<string, Company> companiesByName,
        bool commit,
        ImportResult result)
    {
        var company = companiesByCode.GetValueOrDefault(code) ?? companiesByName.GetValueOrDefault(name);
        var created = company is null;
        company ??= new Company { StartDate = DateTime.Today };

        company.Name = name;
        company.Code = code;
        company.ContactNumber = row["Contact"];
        company.ContactMobile = row["Contact"];
        company.Email = row["Email"];
        company.City = RequiredOrDefault(row["City"], "Dumka");
        company.State = RequiredOrDefault(row["State"], "Jharkhand");
        company.Country = "India";
        company.Address = RequiredOrDefault(row["City"], company.City);
        company.Active = row.Bool("Active", true);
        company.ContactPerson = RequiredOrDefault(company.ContactPerson, "Admin");

        AddLookup(companiesByCode, company.Code, company);
        AddLookup(companiesByName, company.Name, company);

        if (!commit || result.HasLineError(row.Line))
        {
            return;
        }

        if (created)
        {
            db.Companies.Add(company);
            result.Created++;
        }
        else
        {
            result.Updated++;
        }
    }

    private static void ImportStoreGroupRow(
        GarmetixDbContext db,
        CsvDataRow row,
        string name,
        string code,
        Dictionary<string, Company> companiesByCode,
        Dictionary<string, Company> companiesByName,
        Dictionary<string, StoreGroup> groupsByKey,
        bool commit,
        ImportResult result)
    {
        var company = ResolveCompany(row, companiesByCode, companiesByName, result);
        if (company is null)
        {
            return;
        }

        var group = groupsByKey.GetValueOrDefault(GroupKey(company.Id, code)) ?? groupsByKey.GetValueOrDefault(GroupKey(company.Id, name));
        var created = group is null;
        group ??= new StoreGroup { CompanyId = company.Id, StartDate = DateTime.Today };

        group.CompanyId = company.Id;
        group.Name = name;
        group.GroupCode = code;
        group.Active = row.Bool("Active", true);

        AddLookup(groupsByKey, GroupKey(company.Id, group.GroupCode), group);
        AddLookup(groupsByKey, GroupKey(company.Id, group.Name), group);

        if (!commit || result.HasLineError(row.Line))
        {
            return;
        }

        if (created)
        {
            db.StoreGroups.Add(group);
            result.Created++;
        }
        else
        {
            result.Updated++;
        }
    }

    private static void ImportStoreRow(
        GarmetixDbContext db,
        CsvDataRow row,
        string name,
        string code,
        Dictionary<string, Company> companiesByCode,
        Dictionary<string, Company> companiesByName,
        Dictionary<string, StoreGroup> groupsByKey,
        List<Store> stores,
        bool commit,
        ImportResult result)
    {
        var company = ResolveCompany(row, companiesByCode, companiesByName, result);
        if (company is null)
        {
            return;
        }

        var groupCode = row["StoreGroupCode"];
        var group = string.IsNullOrWhiteSpace(groupCode)
            ? groupsByKey.Values.FirstOrDefault(item => item.CompanyId == company.Id)
            : groupsByKey.GetValueOrDefault(GroupKey(company.Id, groupCode));

        if (group is null)
        {
            result.Errors.Add(new ImportRowError(row.Line, "StoreGroupCode", "Store group was not found. Add a StoreGroup row first or enter an existing StoreGroupCode."));
            return;
        }

        var store = stores.FirstOrDefault(item =>
            item.CompanyId == company.Id &&
            item.StoreGroupId == group.Id &&
            (item.StoreCode.Equals(code, StringComparison.OrdinalIgnoreCase) || item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        var created = store is null;
        store ??= new Store { CompanyId = company.Id, StoreGroupId = group.Id, StartDate = DateTime.Today };

        store.CompanyId = company.Id;
        store.StoreGroupId = group.Id;
        store.Name = name;
        store.StoreCode = code;
        store.ContactNumber = row["Contact"];
        store.Email = row["Email"];
        store.City = RequiredOrDefault(row["City"], "Dumka");
        store.State = RequiredOrDefault(row["State"], "Jharkhand");
        store.Country = "India";
        store.Address = RequiredOrDefault(row["City"], store.City);
        store.Active = row.Bool("Active", true);

        if (created)
        {
            stores.Add(store);
        }

        if (!commit || result.HasLineError(row.Line))
        {
            return;
        }

        if (created)
        {
            db.Stores.Add(store);
            result.Created++;
        }
        else
        {
            result.Updated++;
        }
    }

    private static async Task ImportInventoryAsync(
        GarmetixDbContext db,
        IReadOnlyList<CsvDataRow> rows,
        bool commit,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var scope = await GetDefaultScopeAsync(db, result, cancellationToken);
        if (scope is null)
        {
            return;
        }

        var category = await db.ProductCategories.FirstOrDefaultAsync(item => item.CompanyId == scope.CompanyId && item.Name == "General", cancellationToken);
        var subCategory = await db.ProductSubCategories.FirstOrDefaultAsync(item => item.CompanyId == scope.CompanyId && item.Name == "General", cancellationToken);
        var tax = await db.Taxes.FirstOrDefaultAsync(cancellationToken);

        if (commit)
        {
            if (category is null)
            {
                category = new InventoryProductCategory { Name = "General", CompanyId = scope.CompanyId };
                db.ProductCategories.Add(category);
            }

            if (subCategory is null)
            {
                subCategory = new InventoryProductSubCategory { Name = "General", CompanyId = scope.CompanyId };
                db.ProductSubCategories.Add(subCategory);
            }

            if (tax is null)
            {
                tax = new Tax { Name = "GST 0", CompositeRate = 0, TaxType = TaxType.GST };
                db.Taxes.Add(tax);
            }
        }
        else if (category is null || subCategory is null || tax is null)
        {
            result.Warnings.Add("General product category, subcategory, or tax will be created during import.");
        }

        foreach (var row in rows)
        {
            var name = row.Required("Product", result);
            var barcode = row.Required("Barcode", result);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(barcode))
            {
                continue;
            }

            var mrp = row.Decimal("MRP", result);
            var taxRate = row.Decimal("TaxRate", result);
            var purchaseQty = row.Decimal("PurchasedQty", result);
            var soldQty = row.Decimal("SoldQty", result);
            var currentStock = row.Decimal("CurrentStock", result);
            var costPrice = row.Decimal("CostPrice", result);
            var unit = row.Enum("Unit", Unit.Nos, result);
            var productType = row.Enum("ProductType", ProductType.Fabric, result);

            if (!commit || result.HasLineError(row.Line))
            {
                continue;
            }

            var product = await db.Products.FirstOrDefaultAsync(item => item.Barcode == barcode, cancellationToken);
            var createdProduct = product is null;
            product ??= new Product
            {
                Name = name,
                Barcode = barcode,
                CompanyId = scope.CompanyId,
                StoreGroupId = scope.StoreGroupId,
                ProductCategoryId = category!.Id,
                ProductSubCategoryId = subCategory!.Id
            };

            product.Name = name;
            product.MRP = mrp;
            product.TaxRate = taxRate;
            product.Unit = unit;
            product.ProductType = productType;
            product.TaxType = TaxType.GST;

            if (createdProduct)
            {
                db.Products.Add(product);
                result.Created++;
            }
            else
            {
                result.Updated++;
            }

            var stock = await db.Stocks.FirstOrDefaultAsync(item =>
                item.StoreId == scope.StoreId &&
                item.Barcode == barcode &&
                !item.IsOFB,
                cancellationToken);
            stock ??= new Stock
            {
                ProductId = product.Id,
                Barcode = barcode,
                CompanyId = scope.CompanyId,
                StoreGroupId = scope.StoreGroupId,
                StoreId = scope.StoreId,
                TaxId = tax!.Id,
                IsOFB = false
            };

            stock.ProductId = product.Id;
            stock.Unit = unit;
            stock.MRP = mrp;
            stock.TaxRate = taxRate;
            stock.TaxType = TaxType.GST;
            stock.TaxId = tax!.Id;
            stock.PurchaseQty = purchaseQty > 0 ? purchaseQty : currentStock + soldQty;
            stock.SoldQty = soldQty;
            stock.CostPrice = costPrice;

            if (stock.Id == Guid.Empty || db.Entry(stock).State == EntityState.Detached)
            {
                db.Stocks.Add(stock);
            }
        }
    }

    private static async Task ImportHrAsync(
        GarmetixDbContext db,
        IReadOnlyList<CsvDataRow> rows,
        bool commit,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var scope = await GetDefaultScopeAsync(db, result, cancellationToken);
        if (scope is null)
        {
            return;
        }

        foreach (var row in rows)
        {
            var employeeName = row.Required("Employee", result);
            var mobile = row.Required("Mobile", result);
            if (string.IsNullOrWhiteSpace(employeeName) || string.IsNullOrWhiteSpace(mobile))
            {
                continue;
            }

            if (mobile.Length is < 10 or > 15)
            {
                result.Errors.Add(new ImportRowError(row.Line, "Mobile", "Mobile must be 10 to 15 digits."));
                continue;
            }

            var nameParts = employeeName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = nameParts.FirstOrDefault() ?? employeeName;
            var lastName = string.Join(' ', nameParts.Skip(1));
            var joiningDate = row.Date("JoiningDate", DateTime.Today, result);
            var leavingDate = row.OptionalDate("LeavingDate", result);
            var category = row.Enum("Category", EmployeeCategory.Salesman, result);
            var working = row.Bool("Working", true);

            if (!commit || result.HasLineError(row.Line))
            {
                continue;
            }

            var employee = await db.Employees.FirstOrDefaultAsync(item => item.Mobile == mobile, cancellationToken);
            var created = employee is null;
            employee ??= new Employee
            {
                FirstName = firstName,
                LastName = lastName,
                Mobile = mobile,
                Aadhar = mobile[^10..],
                CompanyId = scope.CompanyId,
                StoreGroupId = scope.StoreGroupId,
                StoreId = scope.StoreId
            };

            employee.FirstName = firstName;
            employee.LastName = lastName;
            employee.Email = row["Email"];
            employee.JoiningDate = joiningDate;
            employee.LeavingDate = leavingDate;
            employee.Working = working;
            employee.Category = category;
            employee.DateOfBirth = new DateTime(1990, 1, 1);
            employee.Gender = Gender.Male;

            if (created)
            {
                db.Employees.Add(employee);
                result.Created++;
            }
            else
            {
                result.Updated++;
            }
        }
    }

    private static async Task ImportVouchersAsync(
        GarmetixDbContext db,
        IReadOnlyList<CsvDataRow> rows,
        bool commit,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var scope = await GetDefaultScopeAsync(db, result, cancellationToken);
        if (scope is null)
        {
            return;
        }

        foreach (var row in rows)
        {
            var voucherNumber = row.Required("VoucherNumber", result);
            var party = row.Required("Party", result);
            var particulars = row.Required("Particulars", result);
            if (string.IsNullOrWhiteSpace(voucherNumber) || string.IsNullOrWhiteSpace(party) || string.IsNullOrWhiteSpace(particulars))
            {
                continue;
            }

            var onDate = row.Date("Date", DateTime.Today, result);
            var type = row.Enum("Type", VoucherType.Payment, result);
            var paymentMode = row.Enum("PaymentMode", PaymentMode.Cash, result);
            var amount = row.Decimal("Amount", result);

            if (!commit || result.HasLineError(row.Line))
            {
                continue;
            }

            var voucher = await db.Vouchers.FirstOrDefaultAsync(item => item.StoreId == scope.StoreId && item.VoucherNumber == voucherNumber, cancellationToken);
            var created = voucher is null;
            voucher ??= new Voucher
            {
                VoucherNumber = voucherNumber,
                PartyName = party,
                Particulars = particulars,
                CompanyId = scope.CompanyId,
                StoreGroupId = scope.StoreGroupId,
                StoreId = scope.StoreId
            };

            voucher.OnDate = onDate;
            voucher.VoucherType = type;
            voucher.PartyName = party;
            voucher.Particulars = particulars;
            voucher.PaymentMode = paymentMode;
            voucher.Amount = amount;
            voucher.Remarks = row["Remarks"];

            if (created)
            {
                db.Vouchers.Add(voucher);
                result.Created++;
            }
            else
            {
                result.Updated++;
            }
        }
    }

    private static async Task ImportPettyCashAsync(
        GarmetixDbContext db,
        IReadOnlyList<CsvDataRow> rows,
        bool commit,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var scope = await GetDefaultScopeAsync(db, result, cancellationToken);
        if (scope is null)
        {
            return;
        }

        foreach (var row in rows)
        {
            var onDate = row.Date("Date", DateTime.Today, result);
            var storeId = row.Guid("StoreId", scope.StoreId, result);

            if (!commit || result.HasLineError(row.Line))
            {
                continue;
            }

            var sheet = await db.PettyCashSheets.FirstOrDefaultAsync(item => item.StoreId == storeId && item.OnDate == onDate, cancellationToken);
            var created = sheet is null;
            sheet ??= new PettyCashSheet
            {
                StoreId = storeId,
                OnDate = onDate
            };

            sheet.OpeningBalance = row.Decimal("Opening", result);
            sheet.Sales = row.Decimal("Sales", result);
            sheet.Receipts = row.Decimal("Receipts", result);
            sheet.Expenses = row.Decimal("Expenses", result);
            sheet.Payments = row.Decimal("Payments", result);
            sheet.BankDeposit = row.Decimal("BankDeposit", result);
            sheet.CashInHand = row.Decimal("CashInHand", result);

            if (created)
            {
                db.PettyCashSheets.Add(sheet);
                result.Created++;
            }
            else
            {
                result.Updated++;
            }
        }
    }

    private static UserScope ResolveUserScope(
        CsvDataRow row,
        IReadOnlyList<Company> companies,
        IReadOnlyList<StoreGroup> groups,
        IReadOnlyList<Store> stores,
        ImportResult result)
    {
        var companyCode = row["CompanyCode"];
        var groupCode = row["StoreGroupCode"];
        var storeCode = row["StoreCode"];

        var company = string.IsNullOrWhiteSpace(companyCode)
            ? companies.FirstOrDefault()
            : companies.FirstOrDefault(item => item.Code.Equals(companyCode, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(companyCode) && company is null)
        {
            result.Errors.Add(new ImportRowError(row.Line, "CompanyCode", "Company was not found."));
        }

        var group = string.IsNullOrWhiteSpace(groupCode)
            ? groups.FirstOrDefault(item => company is null || item.CompanyId == company.Id)
            : groups.FirstOrDefault(item =>
                item.GroupCode.Equals(groupCode, StringComparison.OrdinalIgnoreCase) &&
                (company is null || item.CompanyId == company.Id));

        if (!string.IsNullOrWhiteSpace(groupCode) && group is null)
        {
            result.Errors.Add(new ImportRowError(row.Line, "StoreGroupCode", "Store group was not found."));
        }

        var store = string.IsNullOrWhiteSpace(storeCode)
            ? stores.FirstOrDefault(item =>
                (company is null || item.CompanyId == company.Id) &&
                (group is null || item.StoreGroupId == group.Id))
            : stores.FirstOrDefault(item =>
                item.StoreCode.Equals(storeCode, StringComparison.OrdinalIgnoreCase) &&
                (company is null || item.CompanyId == company.Id) &&
                (group is null || item.StoreGroupId == group.Id));

        if (!string.IsNullOrWhiteSpace(storeCode) && store is null)
        {
            result.Errors.Add(new ImportRowError(row.Line, "StoreCode", "Store was not found."));
        }

        return new UserScope(company?.Id, group?.Id, store?.Id);
    }

    private static DefaultScope ResolveRequiredStoreScope(
        CsvDataRow row,
        IReadOnlyList<Company> companies,
        IReadOnlyList<StoreGroup> groups,
        IReadOnlyList<Store> stores,
        ImportResult result)
    {
        var companyCode = row["CompanyCode"];
        var groupCode = row["StoreGroupCode"];
        var storeCode = row["StoreCode"];

        var company = string.IsNullOrWhiteSpace(companyCode)
            ? companies.FirstOrDefault()
            : companies.FirstOrDefault(item => item.Code.Equals(companyCode, StringComparison.OrdinalIgnoreCase));

        if (company is null)
        {
            result.Errors.Add(new ImportRowError(row.Line, "CompanyCode", "Company was not found. Run setup first or enter a valid CompanyCode."));
            return new DefaultScope(Guid.Empty, Guid.Empty, Guid.Empty);
        }

        var group = string.IsNullOrWhiteSpace(groupCode)
            ? groups.FirstOrDefault(item => item.CompanyId == company.Id)
            : groups.FirstOrDefault(item => item.CompanyId == company.Id && item.GroupCode.Equals(groupCode, StringComparison.OrdinalIgnoreCase));

        if (group is null)
        {
            result.Errors.Add(new ImportRowError(row.Line, "StoreGroupCode", "Store group was not found."));
            return new DefaultScope(company.Id, Guid.Empty, Guid.Empty);
        }

        var store = string.IsNullOrWhiteSpace(storeCode)
            ? stores.FirstOrDefault(item => item.CompanyId == company.Id && item.StoreGroupId == group.Id)
            : stores.FirstOrDefault(item =>
                item.CompanyId == company.Id &&
                item.StoreGroupId == group.Id &&
                item.StoreCode.Equals(storeCode, StringComparison.OrdinalIgnoreCase));

        if (store is null)
        {
            result.Errors.Add(new ImportRowError(row.Line, "StoreCode", "Store was not found."));
            return new DefaultScope(company.Id, group.Id, Guid.Empty);
        }

        return new DefaultScope(company.Id, group.Id, store.Id);
    }

    private static BankAccount? ResolveBankAccount(
        CsvDataRow row,
        Guid companyId,
        IReadOnlyList<BankAccount> bankAccounts,
        ImportResult result)
    {
        var accountNumber = row["BankAccountNumber"];
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return null;
        }

        var account = bankAccounts.FirstOrDefault(item =>
            item.CompanyId == companyId &&
            item.AccountNumber.Equals(accountNumber, StringComparison.OrdinalIgnoreCase));

        if (account is null)
        {
            result.Errors.Add(new ImportRowError(row.Line, "BankAccountNumber", "Bank account was not found."));
        }

        return account;
    }

    private static async Task<Vendor> GetOrCreateImportVendorAsync(
        GarmetixDbContext db,
        PurchaseImportLine line,
        CancellationToken cancellationToken)
    {
        var mobile = string.IsNullOrWhiteSpace(line.VendorMobile) ? "NA" : line.VendorMobile.Trim();
        var vendor = await db.Vendors.FirstOrDefaultAsync(
            item => item.CompanyId == line.CompanyId && (item.Name == line.VendorName || item.MobileNumber == mobile),
            cancellationToken);

        if (vendor is null)
        {
            vendor = new Vendor
            {
                CompanyId = line.CompanyId,
                Name = line.VendorName,
                Address = "Dumka",
                City = "Dumka",
                ZipCode = "814101",
                MobileNumber = mobile,
                Active = true
            };
            db.Vendors.Add(vendor);
        }

        vendor.GSTIN = string.IsNullOrWhiteSpace(line.Gstin) ? vendor.GSTIN : line.Gstin.Trim();
        return vendor;
    }

    private static async Task<Customer> GetOrCreateImportCustomerAsync(
        GarmetixDbContext db,
        BillingImportLine line,
        CancellationToken cancellationToken)
    {
        var mobile = string.IsNullOrWhiteSpace(line.CustomerMobile) ? "WALKIN" : line.CustomerMobile.Trim();
        var customer = await db.Customers.FirstOrDefaultAsync(
            item => item.CompanyId == line.CompanyId && item.MobileNumber == mobile,
            cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                CompanyId = line.CompanyId,
                Name = string.IsNullOrWhiteSpace(line.CustomerName) ? "Walk-in Customer" : line.CustomerName.Trim(),
                MobileNumber = mobile
            };
            db.Customers.Add(customer);
        }
        else if (!string.IsNullOrWhiteSpace(line.CustomerName) && customer.Name == "Walk-in Customer")
        {
            customer.Name = line.CustomerName.Trim();
        }

        return customer;
    }

    private static Employee? ResolvePayrollEmployee(
        CsvDataRow row,
        IReadOnlyList<Employee> employees,
        ImportResult result)
    {
        var mobile = row["Mobile"];
        if (!string.IsNullOrWhiteSpace(mobile))
        {
            var employeeByMobile = employees.FirstOrDefault(item => item.Mobile.Equals(mobile, StringComparison.OrdinalIgnoreCase));
            if (employeeByMobile is not null)
            {
                return employeeByMobile;
            }

            result.Errors.Add(new ImportRowError(row.Line, "Mobile", "Employee was not found for this mobile number."));
            return null;
        }

        var employeeName = row.Required("Employee", result);
        if (string.IsNullOrWhiteSpace(employeeName))
        {
            return null;
        }

        var matches = employees
            .Where(item => item.StaffName.Equals(employeeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1)
        {
            return matches[0];
        }

        result.Errors.Add(new ImportRowError(row.Line, "Employee", matches.Count == 0
            ? "Employee was not found."
            : "Employee name matches more than one record. Enter Mobile."));
        return null;
    }

    private static async Task<InventoryProductCategory> GetOrCreateGeneralCategoryAsync(
        GarmetixDbContext db,
        Dictionary<Guid, InventoryProductCategory> cache,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(companyId, out var category))
        {
            return category;
        }

        category = await db.ProductCategories.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Name == "General", cancellationToken);
        if (category is null)
        {
            category = new InventoryProductCategory { Name = "General", CompanyId = companyId };
            db.ProductCategories.Add(category);
        }

        cache[companyId] = category;
        return category;
    }

    private static async Task<InventoryProductSubCategory> GetOrCreateGeneralSubCategoryAsync(
        GarmetixDbContext db,
        Dictionary<Guid, InventoryProductSubCategory> cache,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(companyId, out var subCategory))
        {
            return subCategory;
        }

        subCategory = await db.ProductSubCategories.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Name == "General", cancellationToken);
        if (subCategory is null)
        {
            subCategory = new InventoryProductSubCategory { Name = "General", CompanyId = companyId };
            db.ProductSubCategories.Add(subCategory);
        }

        cache[companyId] = subCategory;
        return subCategory;
    }

    private static async Task<Tax> GetOrCreateImportTaxAsync(
        GarmetixDbContext db,
        List<Tax> taxCache,
        decimal taxRate,
        CancellationToken cancellationToken)
    {
        var tax = taxCache.FirstOrDefault(item => item.TaxType == TaxType.GST && item.CompositeRate == taxRate);
        if (tax is not null)
        {
            return tax;
        }

        tax = await db.Taxes.FirstOrDefaultAsync(item => item.TaxType == TaxType.GST && item.CompositeRate == taxRate, cancellationToken);
        if (tax is null)
        {
            tax = new Tax { Name = $"GST {taxRate:0.##}", CompositeRate = taxRate, TaxType = TaxType.GST };
            db.Taxes.Add(tax);
        }

        taxCache.Add(tax);
        return tax;
    }

    private static async Task<Product> GetOrCreateImportProductAsync(
        GarmetixDbContext db,
        PurchaseImportLine line,
        Guid categoryId,
        Guid subCategoryId,
        Tax tax,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(
            item => item.CompanyId == line.CompanyId && item.Barcode == line.Barcode,
            cancellationToken);

        if (product is null)
        {
            product = new Product
            {
                CompanyId = line.CompanyId,
                StoreGroupId = line.StoreGroupId,
                Name = line.ProductName,
                Barcode = line.Barcode,
                Unit = Unit.Pcs,
                ProductType = ProductType.Apparels,
                ProductCategoryId = categoryId,
                ProductSubCategoryId = subCategoryId
            };
            db.Products.Add(product);
        }

        product.Name = line.ProductName;
        product.MRP = line.Mrp;
        product.TaxRate = tax.CompositeRate;
        product.TaxType = tax.TaxType;
        product.Unit = Unit.Pcs;
        product.ProductType = ProductType.Apparels;
        return product;
    }

    private static Company? ResolveCompany(
        CsvDataRow row,
        Dictionary<string, Company> companiesByCode,
        Dictionary<string, Company> companiesByName,
        ImportResult result)
    {
        var companyCode = row["CompanyCode"];
        if (!string.IsNullOrWhiteSpace(companyCode))
        {
            if (companiesByCode.TryGetValue(companyCode, out var company))
            {
                return company;
            }

            result.Errors.Add(new ImportRowError(row.Line, "CompanyCode", "Company was not found. Add a Company row first or enter an existing CompanyCode."));
            return null;
        }

        if (companiesByCode.Count == 1)
        {
            return companiesByCode.Values.First();
        }

        if (companiesByName.Count == 1)
        {
            return companiesByName.Values.First();
        }

        result.Errors.Add(new ImportRowError(row.Line, "CompanyCode", "CompanyCode is required when more than one company exists."));
        return null;
    }

    private static string GroupKey(Guid companyId, string value)
    {
        return $"{companyId:N}:{value.Trim()}";
    }

    private static void AddLookup<T>(Dictionary<string, T> lookup, string? key, T value)
    {
        if (!string.IsNullOrWhiteSpace(key) && !lookup.ContainsKey(key.Trim()))
        {
            lookup[key.Trim()] = value;
        }
    }

    private static string MakeCode(string name)
    {
        var code = new string(name
            .Where(char.IsLetterOrDigit)
            .Take(12)
            .Select(char.ToUpperInvariant)
            .ToArray());

        return string.IsNullOrWhiteSpace(code) ? "MAIN" : code;
    }

    private static string RequiredOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static async Task<DefaultScope?> GetDefaultScopeAsync(GarmetixDbContext db, ImportResult result, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var store = await db.Stores.AsNoTracking().OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        if (company is null || store is null)
        {
            result.Errors.Add(new ImportRowError(1, "Company", "Run quick setup before importing data."));
            return null;
        }

        return new DefaultScope(company.Id, store.StoreGroupId, store.Id);
    }

    private static IResult CsvFile(string fileName, string[] headers, IEnumerable<string?[]> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",", row.Select(Escape)));
        }

        return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    private static string?[] Row(params object?[] values)
    {
        return values.Select(FormatValue).ToArray();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
            double number => number.ToString("0.##", CultureInfo.InvariantCulture),
            float number => number.ToString("0.##", CultureInfo.InvariantCulture),
            bool flag => flag ? "Yes" : "No",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    private sealed record ExportDefinition(
        string Name,
        string[] Headers,
        Func<GarmetixDbContext, CancellationToken, Task<IEnumerable<string?[]>>> Export);

    private sealed record DefaultScope(Guid CompanyId, Guid StoreGroupId, Guid StoreId);

    private sealed record UserScope(Guid? CompanyId, Guid? StoreGroupId, Guid? StoreId);

    private sealed record PurchaseImportLine(
        int Line,
        Guid CompanyId,
        Guid StoreGroupId,
        Guid StoreId,
        string InvoiceNumber,
        string InwardNumber,
        DateTime OnDate,
        string VendorName,
        string VendorMobile,
        string Gstin,
        string ProductName,
        string Barcode,
        decimal Quantity,
        decimal CostPrice,
        decimal Mrp,
        decimal Discount,
        decimal TaxRate,
        decimal PaidAmount,
        PaymentMode PaymentMode,
        Guid? BankAccountId,
        decimal FrightAmount);

    private sealed record BillingImportLine(
        int Line,
        Guid CompanyId,
        Guid StoreGroupId,
        Guid StoreId,
        string InvoiceNumber,
        DateTime OnDate,
        string CustomerName,
        string CustomerMobile,
        string ProductName,
        string Barcode,
        decimal Quantity,
        decimal Mrp,
        decimal Discount,
        decimal PaidAmount,
        PaymentMode PaymentMode,
        Guid? BankAccountId,
        decimal BillDiscount);

    private sealed class ImportResult(string module, bool commit)
    {
        public string Module { get; } = module;
        public bool Commit { get; } = commit;
        public int RowsRead { get; set; }
        public int ValidRows { get; set; }
        public int InvalidRows { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public List<ImportRowError> Errors { get; } = [];
        public List<string> Warnings { get; } = [];

        public bool HasLineError(int line)
        {
            return Errors.Any(error => error.Line == line);
        }
    }

    private sealed record ImportRowError(int Line, string Field, string Message);

    private sealed class CsvDataRow(int line, IReadOnlyList<string> headers, IReadOnlyList<string> values)
    {
        public int Line { get; } = line;
        public bool IsBlank => values.All(string.IsNullOrWhiteSpace);

        public string this[string field]
        {
            get
            {
                var index = headers.ToList().FindIndex(header => string.Equals(header, field, StringComparison.OrdinalIgnoreCase));
                return index >= 0 && index < values.Count ? values[index].Trim() : string.Empty;
            }
        }

        public string Required(string field, ImportResult result)
        {
            var value = this[field];
            if (string.IsNullOrWhiteSpace(value))
            {
                result.Errors.Add(new ImportRowError(Line, field, "Required value is missing."));
            }

            return value;
        }

        public decimal Decimal(string field, ImportResult result)
        {
            var value = this[field];
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ||
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out number))
            {
                return number;
            }

            result.Errors.Add(new ImportRowError(Line, field, "Enter a valid number."));
            return 0;
        }

        public DateTime Date(string field, DateTime defaultValue, ImportResult result)
        {
            var value = this[field];
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue.Date;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
            {
                return date.Date;
            }

            result.Errors.Add(new ImportRowError(Line, field, "Enter a valid date."));
            return defaultValue.Date;
        }

        public DateTime? OptionalDate(string field, ImportResult result)
        {
            var value = this[field];
            return string.IsNullOrWhiteSpace(value) ? null : Date(field, DateTime.Today, result);
        }

        public int SalaryMonth(string field, DateTime defaultDate, ImportResult result)
        {
            var value = this[field];
            if (string.IsNullOrWhiteSpace(value))
            {
                return (defaultDate.Year * 100) + defaultDate.Month;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) &&
                number >= 200001 &&
                number % 100 is >= 1 and <= 12)
            {
                return number;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
            {
                return (date.Year * 100) + date.Month;
            }

            result.Errors.Add(new ImportRowError(Line, field, "Enter month as yyyyMM, yyyy-MM, or month name and year."));
            return (defaultDate.Year * 100) + defaultDate.Month;
        }

        public Guid Guid(string field, Guid defaultValue, ImportResult result)
        {
            var value = this[field];
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (System.Guid.TryParse(value, out var id))
            {
                return id;
            }

            result.Errors.Add(new ImportRowError(Line, field, "Enter a valid id."));
            return defaultValue;
        }

        public bool Bool(string field, bool defaultValue)
        {
            var value = this[field];
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("1", StringComparison.OrdinalIgnoreCase);
        }

        public TEnum Enum<TEnum>(string field, TEnum defaultValue, ImportResult result) where TEnum : struct
        {
            var value = this[field];
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (System.Enum.TryParse<TEnum>(value.Replace(" ", ""), ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            result.Errors.Add(new ImportRowError(Line, field, $"Enter a valid {typeof(TEnum).Name}."));
            return defaultValue;
        }
    }

    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (current == ',' && !inQuotes)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if ((current == '\n' || current == '\r') && !inQuotes)
            {
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row.ToArray());
                row.Clear();
            }
            else
            {
                field.Append(current);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }
}
