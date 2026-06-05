using System.Globalization;
using System.Text;
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
                    .GroupJoin(
                        db.Stocks.AsNoTracking(),
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
            ["InvoiceNumber", "Date", "Customer", "Mobile", "MRP", "Discount", "Tax", "BillAmount", "Paid", "Balance", "Status"],
            async (db, cancellationToken) =>
            {
                var rows = await db.SalesInvoices.AsNoTracking()
                    .OrderByDescending(item => item.OnDate)
                    .ToListAsync(cancellationToken);

                return rows.Select(item => Row(
                    item.InvoiceNumber,
                    item.OnDate,
                    item.CustomerName,
                    item.CustomerMobileNumber,
                    item.MRP,
                    item.DiscountAmount + item.BillDiscountAmount,
                    item.TaxAmount,
                    item.BillAmount,
                    item.PaidAmount,
                    item.BalanceAmount,
                    item.InvoiceStatus));
            }),
        ["purchase"] = new(
            "Purchase",
            ["InvoiceNumber", "InwardNumber", "Date", "Vendor", "GSTIN", "MRP", "Discount", "Tax", "BillAmount", "Status"],
            async (db, cancellationToken) =>
            {
                var rows = await db.PurchaseInvoices.AsNoTracking()
                    .OrderByDescending(item => item.OnDate)
                    .ToListAsync(cancellationToken);

                return rows.Select(item => Row(
                    item.InvoiceNumber,
                    item.InwardNumber,
                    item.OnDate,
                    item.VendorName,
                    item.VendorGSTIN,
                    item.MRP,
                    item.DiscountAmount,
                    item.TaxAmount,
                    item.BillAmount,
                    item.InvoiceStatus));
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
            ["Employee", "Month", "Basic", "HRA", "SpecialAllowance", "Conveyance", "Incentives", "Earnings", "Deductions", "NetSalary"],
            async (db, cancellationToken) =>
            {
                var payslips = await db.SalaryPaySlips.AsNoTracking()
                    .OrderByDescending(item => item.PayPeriodStart)
                    .ToListAsync(cancellationToken);
                var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);

                return payslips.Select(item =>
                {
                    employees.TryGetValue(item.EmployeeId, out var employee);
                    return Row(
                        employee?.StaffName ?? item.EmployeeId.ToString(),
                        item.MonthYear,
                        item.BasicSalary,
                        item.HRA,
                        item.SpecialAllowance,
                        item.ConveyanceAllowance,
                        item.Incentives,
                        item.TotalEarnings,
                        item.TotalDeductions,
                        item.NetSalary);
                });
            }),
        ["access"] = new(
            "Access",
            ["Name", "UserName", "Email", "Password", "Role", "UserType", "Admin", "AppOperation", "CompanyCode", "StoreGroupCode", "StoreCode"],
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
                    item.Admin,
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
            importSupported = item.Key is "setup" or "inventory" or "hr" or "vouchers" or "petty-cash" or "access"
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
            var admin = row.Bool("Admin", role == LoginRole.Admin) || role == LoginRole.Admin;
            var appOperation = row.Enum("AppOperation", AppOperation.All, result);
            var scope = ResolveUserScope(row, companies, groups, stores, result);

            if (user is not null && (user.Admin || user.Role == LoginRole.Admin) && !admin)
            {
                var adminCount = users.Count(item => item.Admin || item.Role == LoginRole.Admin);
                if (adminCount <= 1)
                {
                    result.Errors.Add(new ImportRowError(row.Line, "Admin", "Cannot remove admin access from the last admin user."));
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

            var stock = await db.Stocks.FirstOrDefaultAsync(item => item.StoreId == scope.StoreId && item.Barcode == barcode, cancellationToken);
            stock ??= new Stock
            {
                ProductId = product.Id,
                Barcode = barcode,
                CompanyId = scope.CompanyId,
                StoreGroupId = scope.StoreGroupId,
                StoreId = scope.StoreId,
                TaxId = tax!.Id
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
