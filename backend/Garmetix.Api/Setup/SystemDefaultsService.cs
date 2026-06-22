using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Setup;

public sealed class SystemDefaultsService(GarmetixDbContext db, IConfiguration? configuration = null)
{
    public const string SuperAdminUserName = "garmetix";
    private const string SuperAdminEmail = "superadmin@garmetix.app";
    private const string SuperAdminName = "Garmetix Super Admin";
    private const string DefaultSuperAdminPassword = "Garmetix@1234!";

    public async Task EnsureStartupDefaultsAsync(CancellationToken cancellationToken)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var companyIds = await db.Companies
            .AsNoTracking()
            .Select(company => company.Id)
            .ToListAsync(cancellationToken);

        foreach (var companyId in companyIds)
        {
            await EnsureForCompanyAsync(companyId, cancellationToken);
        }
    }

    public async Task<AppUser> EnsureSuperAdminAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.UserName == SuperAdminUserName || item.IsSuperAdmin, cancellationToken);

        if (user is null)
        {
            user = new AppUser
            {
                Name = SuperAdminName,
                UserName = SuperAdminUserName,
                Email = SuperAdminEmail,
                Password = PasswordHasher.Hash(ResolveSuperAdminPassword()),
                Role = LoginRole.Admin,
                UserType = UserType.Admin,
                Admin = true,
                IsSuperAdmin = true,
                IsActive = true,
                AppOperation = AppOperation.All
            };
            db.Users.Add(user);
        }
        else
        {
            user.Name = string.IsNullOrWhiteSpace(user.Name) ? SuperAdminName : user.Name;
            user.UserName = SuperAdminUserName;
            user.Email = string.IsNullOrWhiteSpace(user.Email) ? SuperAdminEmail : user.Email;
            user.Role = LoginRole.Admin;
            user.UserType = UserType.Admin;
            user.Admin = true;
            user.IsSuperAdmin = true;
            user.IsActive = true;
            user.AppOperation = AppOperation.All;
            user.CompanyId = null;
            user.StoreGroupId = null;
            user.StoreId = null;

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                user.Password = PasswordHasher.Hash(ResolveSuperAdminPassword());
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<AccountingDefaultsResponse> EnsureForCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken)
            ?? throw new InvalidOperationException("Company was not found.");

        var response = await EnsureAccountingDefaultsAsync(company, cancellationToken);
        await EnsureManagerSalesmenForCompanyAsync(company.Id, cancellationToken);
        return response;
    }

    public async Task EnsureManagerSalesmanForStoreAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == storeId, cancellationToken);
        if (store is null)
        {
            return;
        }

        await EnsureManagerSalesmanAsync(store.CompanyId, store.StoreGroupId, store.Id, cancellationToken);
    }

    private async Task<AccountingDefaultsResponse> EnsureAccountingDefaultsAsync(
        Company company,
        CancellationToken cancellationToken)
    {
        var groupsCreated = 0;
        var ledgersCreated = 0;
        var partiesCreated = 0;

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

        await EnsureBanksAsync(cancellationToken);
        await EnsureSystemTransactionsAsync(company.Id, cancellationToken);

        var capital = await GroupAsync("Capital Account", LedgerCategory.CapitalAccount, "Owner, partner, and proprietor capital");
        await GroupAsync("Loans - Secured", LedgerCategory.SecuredLoans, "Secured loans and term loans");
        await GroupAsync("Loans - Unsecured", LedgerCategory.UnsecuredLoans, "Unsecured loans and borrowings");
        var duties = await GroupAsync("Duties & Taxes", LedgerCategory.DutiesAndTaxes, "GST, TDS, TCS, and statutory tax ledgers");
        var currentAssets = await GroupAsync("Current Assets", LedgerCategory.CurrentAssets, "Current assets");
        await GroupAsync("Fixed Assets", LedgerCategory.FixedAssets, "Fixed assets");
        var currentLiabilities = await GroupAsync("Current Liabilities", LedgerCategory.CurrentLiabilities, "Current liabilities");
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
        var pettyExpenses = await GroupAsync("Petty Expenses", LedgerCategory.IndirectExpenses, "Petty cash expenses");
        var noGroup = await GroupAsync("No Group", LedgerCategory.UnCategory, "Default group for uncategorized and temporary party ledgers");
        var vendorGroup = await GroupAsync("Vendors", LedgerCategory.Vendor, "Vendor party ledgers");
        var customerGroup = await GroupAsync("Customers", LedgerCategory.Customer, "Customer party ledgers");
        var employeeGroup = await GroupAsync("Employees", LedgerCategory.Employees, "Employee party ledgers");
        await GroupAsync("Stock", LedgerCategory.Stock, "Stock ledgers");
        await GroupAsync("Debitors", LedgerCategory.Debitor, "Legacy debitor ledgers");
        await GroupAsync("Creditors", LedgerCategory.Creditor, "Legacy creditor ledgers");
        await GroupAsync("Suspense Account", LedgerCategory.SuspenseAccount, "Suspense and temporary posting ledgers");

        await LedgerAsync("Capital Account", capital, LedgerType.CapitalAccount);
        await LedgerAsync("Cash In Hand", cash, LedgerType.Cash);
        await LedgerAsync("Petty Cash", cash, LedgerType.Cash);
        await LedgerAsync("Primary Bank Account", bankAccounts, LedgerType.BankAccount);
        await LedgerAsync("Bank Clearing", bankAccounts, LedgerType.BankAccount);
        await LedgerAsync("Sales", salesAccounts, LedgerType.Sale);
        await LedgerAsync("Sales Return", salesAccounts, LedgerType.Sale);
        await LedgerAsync("Purchases", purchaseAccounts, LedgerType.Purcahase);
        await LedgerAsync("Purchase Return", purchaseAccounts, LedgerType.Purcahase);
        await LedgerAsync("Output GST", duties, LedgerType.CurrentLiability);
        await LedgerAsync("Input GST", currentAssets, LedgerType.CurrentAsset);
        await LedgerAsync("GST Payable", duties, LedgerType.CurrentLiability);
        await LedgerAsync("GST Credit Carry Forward", currentAssets, LedgerType.CurrentAsset);
        await LedgerAsync("Salary Payables", currentLiabilities, LedgerType.CurrentLiability);
        await LedgerAsync("Salary Advance", currentAssets, LedgerType.CurrentAsset);
        await LedgerAsync("Round Off", indirectIncome, LedgerType.Income);
        await LedgerAsync("Discount Allowed", indirectExpenses, LedgerType.IndirectExpenses);
        await LedgerAsync("Discount Received", indirectIncome, LedgerType.Income);
        await LedgerAsync("Dan", pettyExpenses, LedgerType.Expenses);
        await LedgerAsync("Snacks & Tea", snackGroup, LedgerType.Expenses);
        await LedgerAsync("Electricity", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Water", snackGroup, LedgerType.Expenses);
        await LedgerAsync("Printing & Stationery", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Transport & Freight Charges", directExpenses, LedgerType.Expenses);
        await LedgerAsync("Miscellaneous", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Internet & Mobile Bills", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Store Maintenance", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Store Supplies", storeExpenses, LedgerType.Expenses);
        await LedgerAsync("Petty Cash Expenses", pettyExpenses, LedgerType.Expenses);
        var noPartyLedger = await LedgerAsync("No Party", noGroup, LedgerType.Suspense, true);
        await LedgerAsync("Sundry Debtors Control", debtors, LedgerType.SundryDebtor, true);
        await LedgerAsync("Sundry Creditors Control", creditors, LedgerType.SundryCreditor, true);

        if (!await db.Parties.AnyAsync(item => item.CompanyId == company.Id && item.Name == "No Party", cancellationToken))
        {
            db.Parties.Add(new Party
            {
                CompanyId = company.Id,
                Name = "No Party",
                Category = PartyType.Others,
                LedgerId = noPartyLedger.Id
            });
            partiesCreated++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await EnsurePrimaryBankAccountAsync(company, bankAccounts, cancellationToken);
        return new AccountingDefaultsResponse(groupsCreated, ledgersCreated, partiesCreated);
    }

    private async Task EnsureManagerSalesmenForCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var stores = await db.Stores
            .AsNoTracking()
            .Where(store => store.CompanyId == companyId)
            .Select(store => new { store.CompanyId, store.StoreGroupId, store.Id })
            .ToListAsync(cancellationToken);

        foreach (var store in stores)
        {
            await EnsureManagerSalesmanAsync(store.CompanyId, store.StoreGroupId, store.Id, cancellationToken);
        }
    }

    private async Task EnsureManagerSalesmanAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
    {
        var salesman = await db.Salesmen
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.StoreId == storeId && item.Name == "Manager", cancellationToken);

        if (salesman is null)
        {
            db.Salesmen.Add(new Salesman
            {
                Name = "Manager",
                Active = true,
                CompanyId = companyId,
                StoreGroupId = storeGroupId,
                StoreId = storeId,
                CreatedBy = "SystemDefaultSalesman"
            });
        }
        else
        {
            salesman.StoreGroupId = storeGroupId;
            salesman.Active = true;
            salesman.Deleted = false;
            salesman.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBanksAsync(CancellationToken cancellationToken)
    {
        foreach (var bankName in new[] { "State Bank Of India", "HDFC Bank", "ICICI Bank", "Axis Bank", "Punjab National Bank", "Bank of Baroda", "Kotak Mahindra Bank" })
        {
            if (!await db.Banks.AnyAsync(item => item.Name == bankName, cancellationToken))
            {
                db.Banks.Add(new Bank { Name = bankName });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSystemTransactionsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        foreach (var transactionName in new[] { "Petty Cash Expenses", "Home Expenses", "Store Expenses", "Dan & Donations", "Snacks & Breakfast Expenses", "Cash In", "Cash Out" })
        {
            if (!await db.Transactions.AnyAsync(item => item.CompanyId == companyId && item.Name == transactionName, cancellationToken))
            {
                db.Transactions.Add(new Transaction
                {
                    CompanyId = companyId,
                    Name = transactionName,
                    CreatedBy = AccountingDefaultProtection.CreatedByMarker
                });
            }
        }
    }

    private async Task EnsurePrimaryBankAccountAsync(Company company, LedgerGroup bankAccounts, CancellationToken cancellationToken)
    {
        if (await db.BankAccounts.AnyAsync(item => item.CompanyId == company.Id, cancellationToken))
        {
            return;
        }

        var bank = await db.Banks.OrderBy(item => item.Name).FirstAsync(cancellationToken);
        var ledger = await db.Ledgers.FirstAsync(
            item => item.CompanyId == company.Id && item.Name == "Primary Bank Account",
            cancellationToken);

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

    private string ResolveSuperAdminPassword()
        => configuration?["Garmetix:SuperAdminPassword"]
            ?? configuration?["GARMETIX_SUPER_ADMIN_PASSWORD"]
            ?? Environment.GetEnvironmentVariable("GARMETIX_SUPER_ADMIN_PASSWORD")
            ?? DefaultSuperAdminPassword;
}
