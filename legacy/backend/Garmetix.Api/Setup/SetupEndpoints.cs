using Garmetix.Api.Auth;
using Garmetix.Api.Accounting;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
using InventoryProductSubCategory = Garmetix.Core.Models.Inventory.ProductSubCategory;

namespace Garmetix.Api.Setup;

public static class SetupEndpoints
{
    public static RouteGroupBuilder MapSetupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/setup")
            .WithTags("Setup")
            .RequireAuthorization();

        group.MapGet("/status", GetStatusAsync);
        group.MapPost("/quick-start", QuickStartAsync).RequireAuthorization(GarmetixPolicies.CompanySetup);
        group.MapPost("/quick-product", QuickProductAsync).RequireAuthorization(GarmetixPolicies.Inventory);
        group.MapPost("/accounting-defaults", SeedAccountingDefaultsAsync).RequireAuthorization(GarmetixPolicies.Accounting);

        return group;
    }

    private static async Task<SetupStatusResponse> GetStatusAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var company = await WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context).OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var storeGroup = await WorkspaceScope.ApplyTo(db.StoreGroups.AsNoTracking(), context).OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context).OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        return new SetupStatusResponse(
            company is not null,
            storeGroup is not null,
            store is not null,
            await WorkspaceScope.ApplyTo(db.ProductCategories.AsNoTracking(), context).AnyAsync(cancellationToken),
            await db.Taxes.AnyAsync(cancellationToken),
            company?.Id,
            storeGroup?.Id,
            store?.Id);
    }

    private static async Task<IResult> QuickStartAsync(QuickSetupRequest request, GarmetixDbContext db, SystemDefaultsService systemDefaults, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            company = new Company
            {
                Name = RequiredOrDefault(request.CompanyName, "Garmetix Company"),
                Code = "MAIN",
                Active = true,
                ContactNumber = request.ContactNumber ?? string.Empty,
                Email = request.Email ?? string.Empty,
                Address = RequiredOrDefault(request.City, "Dumka"),
                City = RequiredOrDefault(request.City, "Dumka"),
                State = RequiredOrDefault(request.State, "Jharkhand"),
                Country = "India",
                ZipCode = RequiredOrDefault(request.ZipCode, "814101"),
                ContactPerson = "Admin",
                ContactMobile = request.ContactNumber ?? string.Empty
            };
            db.Companies.Add(company);
        }

        var storeGroup = await db.StoreGroups.FirstOrDefaultAsync(item => item.CompanyId == company.Id, cancellationToken);
        if (storeGroup is null)
        {
            storeGroup = new StoreGroup
            {
                Name = RequiredOrDefault(request.StoreGroupName, "Main Group"),
                GroupCode = "MAIN",
                Active = true,
                CompanyId = company.Id
            };
            db.StoreGroups.Add(storeGroup);
        }

        var store = await db.Stores.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.StoreGroupId == storeGroup.Id, cancellationToken);
        if (store is null)
        {
            store = new Store
            {
                Name = RequiredOrDefault(request.StoreName, "Main Store"),
                StoreCode = "MAIN",
                Active = true,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                ContactNumber = request.ContactNumber ?? string.Empty,
                Email = request.Email ?? string.Empty,
                Address = RequiredOrDefault(request.City, "Dumka"),
                City = RequiredOrDefault(request.City, "Dumka"),
                State = RequiredOrDefault(request.State, "Jharkhand"),
                Country = "India",
                ZipCode = RequiredOrDefault(request.ZipCode, "814101")
            };
            db.Stores.Add(store);
        }

        var category = await db.ProductCategories.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Name == "General", cancellationToken);
        if (category is null)
        {
            category = new InventoryProductCategory
            {
                Name = "General",
                CompanyId = company.Id
            };
            db.ProductCategories.Add(category);
        }

        var subCategory = await db.ProductSubCategories.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Name == "General", cancellationToken);
        if (subCategory is null)
        {
            subCategory = new InventoryProductSubCategory
            {
                Name = "General",
                CompanyId = company.Id
            };
            db.ProductSubCategories.Add(subCategory);
        }

        var tax = await db.Taxes.FirstOrDefaultAsync(item => item.Name == "GST 5", cancellationToken);
        if (tax is null)
        {
            tax = new Tax
            {
                Name = "GST 5",
                CompositeRate = 5,
                TaxType = TaxType.GST
            };
            db.Taxes.Add(tax);
        }

        var salesman = await db.Salesmen.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.StoreId == store.Id && item.Name == "Manager", cancellationToken);
        if (salesman is null)
        {
            db.Salesmen.Add(new Salesman
            {
                Name = "Manager",
                Active = true,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                StoreId = store.Id
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await systemDefaults.EnsureForCompanyAsync(company.Id, cancellationToken);

        return Results.Ok(new QuickSetupResponse(company.Id, storeGroup.Id, store.Id, category.Id, subCategory.Id, tax.Id));
    }

    private static async Task<IResult> QuickProductAsync(QuickProductRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Barcode))
        {
            return Results.BadRequest(new { message = "Product name and barcode are required." });
        }

        var categoryId = request.ProductCategoryId ?? await db.ProductCategories
            .Where(item => item.CompanyId == request.CompanyId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var subCategoryId = request.ProductSubCategoryId ?? await db.ProductSubCategories
            .Where(item => item.CompanyId == request.CompanyId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var tax = request.TaxId.HasValue
            ? await db.Taxes.FirstOrDefaultAsync(item => item.Id == request.TaxId.Value, cancellationToken)
            : await db.Taxes.FirstOrDefaultAsync(cancellationToken);

        if (categoryId == Guid.Empty || subCategoryId == Guid.Empty || tax is null)
        {
            return Results.BadRequest(new { message = "Run quick setup before adding products." });
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Barcode = request.Barcode.Trim(),
            MRP = request.Mrp,
            Descriptions = string.IsNullOrWhiteSpace(request.Descriptions) ? null : request.Descriptions.Trim(),
            HSNCode = string.IsNullOrWhiteSpace(request.HSNCode) ? null : request.HSNCode.Trim(),
            TaxRate = tax.CompositeRate,
            TaxType = tax.TaxType,
            Unit = request.Unit ?? Unit.Pcs,
            ProductType = request.ProductType ?? ProductType.Fabric,
            ProductGroup = request.ProductGroup ?? ProductGroup.Shirting,
            ProductCategoryId = categoryId,
            ProductSubCategoryId = subCategoryId,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId
        };

        var stock = new Stock
        {
            ProductId = product.Id,
            Barcode = product.Barcode,
            HSNCode = product.HSNCode,
            Unit = product.Unit,
            PurchaseQty = request.OpeningQuantity,
            CostPrice = request.CostPrice ?? 0,
            MRP = request.Mrp,
            TaxRate = tax.CompositeRate,
            TaxType = tax.TaxType,
            TaxId = tax.Id,
            StockType = request.StockType ?? StockType.Billed,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId
        };

        var productWritable = WorkspaceScope.CanWrite(product, context, out var productScopeMessage);
        var stockWritable = WorkspaceScope.CanWrite(stock, context, out var stockScopeMessage);
        if (!productWritable || !stockWritable)
        {
            return Results.BadRequest(new { message = productScopeMessage ?? stockScopeMessage ?? "Selected company/store is outside your access scope." });
        }

        db.Products.Add(product);
        db.Stocks.Add(stock);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/products/{product.Id}", new { product, stock });
    }

    private static string RequiredOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static async Task<IResult> SeedAccountingDefaultsAsync(
        Guid? companyId,
        GarmetixDbContext db,
        SystemDefaultsService systemDefaults,
        CancellationToken cancellationToken)
    {
        var company = companyId.HasValue
            ? await db.Companies.FirstOrDefaultAsync(item => item.Id == companyId.Value, cancellationToken)
            : await db.Companies.OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return Results.BadRequest(new { message = "Select a valid company before creating accounting defaults." });
        }

        var result = await systemDefaults.EnsureForCompanyAsync(company.Id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<AccountingDefaultsResponse> EnsureAccountingDefaultsAsync(
        GarmetixDbContext db,
        Company company,
        CancellationToken cancellationToken)
    {
        var groupsCreated = 0;
        var ledgersCreated = 0;
        var partiesCreated = 0;
        var defaultTransactions = new[]
        {
            "Petty Cash Expenses",
            "Home Expenses",
            "Store Expenses",
            "Dan & Donations",
            "Snacks & Breakfast Expenses",
            "Cash In",
            "Cash Out"
        };

        async Task<LedgerGroup> GroupAsync(string name, LedgerCategory category, string remarks)
        {
            var group = await db.LedgerGroups.FirstOrDefaultAsync(
                item => item.CompanyId == company.Id && item.Name == name,
                cancellationToken);

            if (group is not null)
            {
                if (string.IsNullOrWhiteSpace(group.CreatedBy))
                {
                    group.CreatedBy = AccountingDefaultProtection.CreatedByMarker;
                }
                return group;
            }

            group = new LedgerGroup
            {
                CompanyId = company.Id,
                Name = name,
                Category = category,
                Remarks = remarks,
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            };
            db.LedgerGroups.Add(group);
            groupsCreated++;
            return group;
        }

        async Task<Ledger> LedgerAsync(string name, LedgerGroup group, LedgerType type, bool isParty = false)
        {
            var ledger = await db.Ledgers.FirstOrDefaultAsync(
                item => item.CompanyId == company.Id && item.Name == name,
                cancellationToken);

            if (ledger is not null)
            {
                if (string.IsNullOrWhiteSpace(ledger.CreatedBy))
                {
                    ledger.CreatedBy = AccountingDefaultProtection.CreatedByMarker;
                }
                return ledger;
            }

            ledger = new Ledger
            {
                CompanyId = company.Id,
                Name = name,
                LedgerGroupId = group.Id,
                LedgerType = type,
                OpeningBalance = 0,
                OpeningDate = company.StartDate,
                IsParty = isParty,
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            };
            db.Ledgers.Add(ledger);
            ledgersCreated++;
            return ledger;
        }

        await EnsureBanksAsync(db, cancellationToken);
        foreach (var transactionName in defaultTransactions)
        {
            if (!await db.Transactions.AnyAsync(
                item => item.CompanyId == company.Id && item.Name == transactionName,
                cancellationToken))
            {
                db.Transactions.Add(new Transaction
                {
                    CompanyId = company.Id,
                    Name = transactionName
                });
            }
        }

        await GroupAsync("Capital Account", LedgerCategory.CapitalAccount, "Owner capital, partner capital, and proprietor capital");
        await GroupAsync("Loans - Secured", LedgerCategory.SecuredLoans, "Secured loans and term loans");
        await GroupAsync("Loans - Unsecured", LedgerCategory.UnsecuredLoans, "Unsecured loans and borrowings");
        await GroupAsync("Duties & Taxes", LedgerCategory.DutiesAndTaxes, "GST, TDS, TCS, and statutory tax ledgers");
        await GroupAsync("Current Assets", LedgerCategory.CurrentAssets, "Current assets");
        await GroupAsync("Fixed Assets", LedgerCategory.FixedAssets, "Fixed assets");
        await GroupAsync("Current Liabilities", LedgerCategory.CurrentLiabilities, "Current liabilities");
        var debtors = await GroupAsync("Sundry Debtors", LedgerCategory.SundryDebtors, "Customer receivables");
        var creditors = await GroupAsync("Sundry Creditors", LedgerCategory.SundryCreditors, "Vendor and supplier payables");
        var bankAccounts = await GroupAsync("Bank Accounts", LedgerCategory.BankAccounts, "Current, savings, cash credit, and overdraft bank accounts");
        var cash = await GroupAsync("Cash-in-Hand", LedgerCategory.CashInHand, "Cash counters and cash in hand");
        var directIncome = await GroupAsync("Direct Income", LedgerCategory.DirectIncome, "Direct income");
        var indirectIncome = await GroupAsync("Indirect Income", LedgerCategory.IndirectIncome, "Indirect income");
        var directExpenses = await GroupAsync("Direct Expenses", LedgerCategory.DirectExpenses, "Direct business expenses");
        var indirectExpenses = await GroupAsync("Indirect Expenses", LedgerCategory.IndirectExpenses, "Indirect business expenses");
        var purchaseAccounts = await GroupAsync("Purchase Accounts", LedgerCategory.PurchaseAccounts, "Purchase and purchase return accounts");
        var salesAccounts = await GroupAsync("Sales Accounts", LedgerCategory.SalesAccounts, "Sales and sales return accounts");
        var snackGroup = await GroupAsync("Snacks & Refreshments", LedgerCategory.IndirectExpenses, "Store snacks and refreshments expenses");
        var storeExpenses = await GroupAsync("Store Expenses", LedgerCategory.IndirectExpenses, "Store expenses");
        var pettyExpenses = await GroupAsync("Petty Expenses", LedgerCategory.IndirectExpenses, "Petty expenses");
        var noGroup = await GroupAsync("No Group", LedgerCategory.UnCategory, "Default group for uncategorized and temporary party ledgers");
        var vendorGroup = await GroupAsync("Vendors", LedgerCategory.Vendor, "Vendor party ledgers");
        var customerGroup = await GroupAsync("Customers", LedgerCategory.Customer, "Customer party ledgers");
        var employeeGroup = await GroupAsync("Employees", LedgerCategory.Employees, "Employee party ledgers");
        await GroupAsync("Stock", LedgerCategory.Stock, "Stock ledgers");
        await GroupAsync("Debitors", LedgerCategory.Debitor, "Legacy debitor ledgers");
        await GroupAsync("Creditors", LedgerCategory.Creditor, "Legacy creditor ledgers");

        await LedgerAsync("Dan", pettyExpenses, LedgerType.Expenses);
        await LedgerAsync("Snacks & Tea", snackGroup, LedgerType.Expenses);
        await LedgerAsync("Electricity", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Water", snackGroup, LedgerType.Expenses);
        await LedgerAsync("Printing & Stationery", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Transport & Freight Charges", directExpenses, LedgerType.Expenses);
        await LedgerAsync("Miscellaneous", storeExpenses, LedgerType.Expenses);
        var noPartyLedger = await LedgerAsync("No Party", noGroup, LedgerType.Suspense, true);
        await LedgerAsync("Cash In Hand", cash, LedgerType.Cash);
        await LedgerAsync("Salary Payables", directExpenses, LedgerType.Expenses);
        await LedgerAsync("Internet & Mobile Bills", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Store Maintenance", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Store Supplies", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Petty Cash Expenses", pettyExpenses, LedgerType.Expenses);
        await LedgerAsync("Sales", salesAccounts, LedgerType.Sale);
        await LedgerAsync("Sales Return", salesAccounts, LedgerType.Sale);
        await LedgerAsync("Purchases", purchaseAccounts, LedgerType.Purcahase);
        await LedgerAsync("Purchase Return", purchaseAccounts, LedgerType.Purcahase);
        await LedgerAsync("Bank Clearing", bankAccounts, LedgerType.BankAccount);
        await LedgerAsync("Sundry Debtors Control", debtors, LedgerType.SundryDebtor, true);
        await LedgerAsync("Sundry Creditors Control", creditors, LedgerType.SundryCreditor, true);

        var noParty = await db.Parties.FirstOrDefaultAsync(
            item => item.CompanyId == company.Id && item.Name == "No Party",
            cancellationToken);

        if (noParty is null)
        {
            noParty = new Party
            {
                CompanyId = company.Id,
                Name = "No Party",
                Category = PartyType.Others,
                LedgerId = noPartyLedger.Id
            };
            db.Parties.Add(noParty);
            partiesCreated++;
        }

        async Task<Party> PartyFromMasterAsync(
            string name,
            PartyType partyType,
            LedgerGroup ledgerGroup,
            LedgerType ledgerType,
            string? address,
            string? email,
            string? phone,
            string? gstin,
            string? pan)
        {
            var ledgerName = $"{name} - {partyType}";
            var ledger = await LedgerAsync(ledgerName, ledgerGroup, ledgerType, true);
            var party = await db.Parties.FirstOrDefaultAsync(
                item => item.CompanyId == company.Id && item.Name == name && item.Category == partyType,
                cancellationToken);

            if (party is null)
            {
                party = new Party
                {
                    CompanyId = company.Id,
                    Name = name,
                    Category = partyType,
                    LedgerId = ledger.Id,
                    Address = address,
                    EmailId = email,
                    Phone = phone,
                    GSTIN = gstin,
                    PAN = pan
                };
                db.Parties.Add(party);
                partiesCreated++;
            }
            else if (party.LedgerId == Guid.Empty)
            {
                party.LedgerId = ledger.Id;
            }

            return party;
        }

        var customers = await db.Customers.Where(item => item.CompanyId == company.Id).ToListAsync(cancellationToken);
        foreach (var customer in customers)
        {
            var party = await PartyFromMasterAsync(
                customer.Name,
                PartyType.Customer,
                customerGroup,
                LedgerType.SundryDebtor,
                customer.Address,
                customer.Email,
                customer.MobileNumber,
                customer.GSTIN,
                null);
            customer.PartyId = party.Id;
        }

        var vendors = await db.Vendors.Where(item => item.CompanyId == company.Id).ToListAsync(cancellationToken);
        foreach (var vendor in vendors)
        {
            var party = await PartyFromMasterAsync(
                vendor.Name,
                PartyType.Vendor,
                vendorGroup,
                LedgerType.SundryCreditor,
                vendor.Address,
                vendor.Email,
                vendor.MobileNumber,
                vendor.GSTIN,
                vendor.Pan);
            vendor.PartyId = party.Id;
        }

        var employees = await db.Employees.Where(item => item.CompanyId == company.Id).ToListAsync(cancellationToken);
        foreach (var employee in employees)
        {
            await PartyFromMasterAsync(
                employee.StaffName,
                PartyType.Employee,
                employeeGroup,
                LedgerType.Employee,
                null,
                employee.Email,
                employee.Mobile,
                null,
                employee.PAN);
        }

        await db.SaveChangesAsync(cancellationToken);
        await EnsurePrimaryBankAccountAsync(db, company, bankAccounts, cancellationToken);
        return new AccountingDefaultsResponse(groupsCreated, ledgersCreated, partiesCreated);
    }

    private static async Task EnsureBanksAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        foreach (var bankName in new[] { "State Bank Of India", "HDFC Bank", "ICICI Bank", "Axis Bank", "Punjab National Bank", "Bank of Baroda", "Kotak Mahindra Bank" })
        {
            if (!await db.Banks.AnyAsync(item => item.Name == bankName, cancellationToken))
            {
                db.Banks.Add(new Bank { Name = bankName });
            }
        }
    }

    private static async Task EnsurePrimaryBankAccountAsync(
        GarmetixDbContext db,
        Company company,
        LedgerGroup bankAccounts,
        CancellationToken cancellationToken)
    {
        if (await db.BankAccounts.AnyAsync(item => item.CompanyId == company.Id, cancellationToken))
        {
            return;
        }

        var bank = await db.Banks.FirstAsync(cancellationToken);
        var ledger = await db.Ledgers.FirstOrDefaultAsync(
            item => item.CompanyId == company.Id && item.Name == "Primary Bank Account",
            cancellationToken);

        if (ledger is null)
        {
            ledger = new Ledger
            {
                CompanyId = company.Id,
                LedgerGroupId = bankAccounts.Id,
                LedgerType = LedgerType.BankAccount,
                Name = "Primary Bank Account",
                OpeningBalance = 0,
                OpeningDate = company.StartDate,
                IsParty = false,
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            };
            db.Ledgers.Add(ledger);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.BankAccounts.Add(new BankAccount
        {
            CompanyId = company.Id,
            BankId = bank.Id,
            LedgerId = ledger.Id,
            AccountHolderName = company.Name,
            AccountNumber = "PRIMARY-BANK",
            AccountType = AccountType.Current,
            Branch = company.City,
            IFSCode = "CHANGE-ME",
            OpeningDate = company.StartDate,
            Active = true
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
