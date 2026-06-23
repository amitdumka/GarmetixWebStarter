using Garmetix.Api.Auth;
using Garmetix.Api.Accounting;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
using InventoryProductSubCategory = Garmetix.Core.Models.Inventory.ProductSubCategory;

namespace Garmetix.Api.Seeds;

public sealed class AfssDefaultSeederService(GarmetixDbContext db)
{
    public static IReadOnlyList<AfssSeedProfileDto> Profiles { get; } =
    [
        new(
            "AF",
            "Aadwika Fashion - Amit Kumar",
            "Aadwika Fashion",
            "AF",
            "Amit Kumar",
            "20AJHPA7396P1ZV",
            "AJHPA7396P",
            "MBO",
            "Aadwika Fashion MBO",
            "AFMBO",
            "Aadwika Fashion MBO Dumka",
            "Dumka",
            "Jharkhand",
            "814101",
            "aadwikafashion@gmail.com",
            "9334799099",
            "aadwikafashion.com",
            "Seeder.cs + seeder2.cs"),
        new(
            "AFS",
            "Aadwika Fashion - Shalini Kumari",
            "Aadwika Fashion - Shalini",
            "AFS",
            "Shalini Kumari",
            "20CLEPK0467L1Z8",
            "CLEPK0467L",
            "MBO",
            "Aadwika Fashion MBO",
            "AFSMBO",
            "Aadwika Fashion MBO Dumka",
            "Dumka",
            "Jharkhand",
            "814101",
            "aadwikafashion.mbo@gmail.com",
            "8409201476",
            "aadwikafashion.in",
            "Seeder.cs + seeder2.cs"),
        new(
            "SM",
            "Smart Menswear - under Aadwika Fashion",
            "Aadwika Fashion",
            "AF",
            "Amit Kumar",
            "20AJHPA7396P1ZV",
            "AJHPA7396P",
            "MBO",
            "Aadwika Fashion MBO",
            "SM01",
            "Smart Menswear",
            "Dumka",
            "Jharkhand",
            "814101",
            "smartmenswear@aadwikafashion.in",
            "9334799099",
            "aadwikafashion.com",
            "seeder2.cs only")
    ];

    public static AfssSeederComparisonDto Comparison { get; } = new(
        CommonParts:
        [
            "Company/store/store-group profile structure for Aadwika Fashion AF and AFS.",
            "Default banking master: State Bank Of India, ICICI Bank, HDFC Bank, Bank of Baroda, Kotka Bank, Punjab National Bank.",
            "Default GST/IGST tax rates: GST 5/12/18, IGST 5/12/18, plus CGST/SGST display rows.",
            "Default transactions: Petty Cash Expenses, Home Expenses, Store Expenses, Dan & Donations, Snacks & Breakfast Expenses, Cash In, Cash Out.",
            "Default ledger groups and ledgers for expenses, cash, sales, purchase, stock, vendors, customers, employees, debtors/creditors, banks, and capital/loan groups.",
            "Owner, store manager, accountant employee records, default Manager salesman, and Admin/Owner/StoreManager users."
        ],
        SeederCsOnly:
        [
            "SeedProducts created one old sample product/stock row.",
            "Synchronous SaveChanges/count/saved status tracking.",
            "Old MAUI notification calls during seeding."
        ],
        Seeder2CsOnly:
        [
            "Async seeding flow with StringBuilder messages and per-section exception handling.",
            "Smart Menswear profile merges into Aadwika Fashion company under Aadwika Fashion MBO store group.",
            "Safer name split logic for owner/employees.",
            "Corrected SGST 2.5% label spelling."
        ],
        ModelAdjustmentsApplied:
        [
            "ProductCategory is treated as the inventory model, not the obsolete enum.",
            "Products now seed ProductGroup, HSNCode, ProductType.Readymade/Shoes/Fabric, category/subcategory IDs, and ProductDetail rows.",
            "Stock rows include required Barcode, HSNCode, Unit, TaxId, TaxType, StockType.Opening, StoreGroupId, and StoreId.",
            "Opening stock also creates StockMovement rows so the Stage 4 stock ledger remains auditable.",
            "Users are seeded with PBKDF2 password hashes instead of old plain text passwords."
        ]);

    public async Task<AfssSeedResponse> SeedAsync(AfssSeedRequest request, CancellationToken cancellationToken)
    {
        var profile = Profiles.FirstOrDefault(item => item.Code.Equals(request.ProfileCode, StringComparison.OrdinalIgnoreCase))
            ?? Profiles[0];
        var notes = new List<string>
        {
            "AF/SS seed is idempotent: existing rows are reused by company/name/code/barcode instead of duplicated.",
            "This web seeder merges Aadwika Fashion Amit Kumar and Smart Menswear into one company/store-group structure, while Aadwika Fashion - Shalini remains separate.",
            "The old MAUI multi-database creation and notification calls are intentionally not ported to the web app."
        };
        var counters = new AfssSeedCounters();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var company = await ResolveSeedCompanyAsync(request, profile, counters, cancellationToken);

        PatchCompanyDefaults(company, profile);
        var storeGroup = await EnsureStoreGroupAsync(company, profile, counters, cancellationToken);
        var store = await EnsureStoreAsync(company, storeGroup, profile, counters, cancellationToken);

        await EnsureBanksAsync(counters, cancellationToken);
        await EnsureTaxesAsync(counters, cancellationToken);
        await EnsureTransactionsAsync(company, counters, cancellationToken);
        var ledgerContext = await EnsureLedgerGroupsAndLedgersAsync(company, counters, cancellationToken);
        await EnsureSbiCurrentAccountAsync(company, ledgerContext.BankGroup, counters, cancellationToken);

        if (request.IncludeEmployees)
        {
            await EnsureEmployeesAndSalesmanAsync(company, storeGroup, store, profile, counters, cancellationToken);
        }

        if (request.IncludeUsers)
        {
            await EnsureUsersAsync(company, storeGroup, store, profile, request.ResetDefaultUserPasswords, counters, cancellationToken);
        }

        if (request.IncludeProducts)
        {
            await EnsureProductMastersAsync(company, storeGroup, store, profile, counters, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var existing = await BuildExistingCountsAsync(company.Id, storeGroup.Id, store.Id, cancellationToken);

            return new AfssSeedResponse(
                "AF/SS default seed completed.",
                DateTimeOffset.UtcNow,
                profile,
                new AfssSeedTargetDto(company.Id, company.Name, storeGroup.Id, storeGroup.Name, store.Id, store.Name),
                counters.ToCreatedDto(),
                existing,
                notes);
        });
    }

private async Task<Company> ResolveSeedCompanyAsync(
    AfssSeedRequest request,
    AfssSeedProfileDto profile,
    AfssSeedCounters counters,
    CancellationToken cancellationToken)
{
    Company? company = null;
    if (request.CompanyId.HasValue && request.CompanyId.Value != Guid.Empty)
    {
        company = await db.Companies.FirstOrDefaultAsync(item => item.Id == request.CompanyId.Value, cancellationToken);
        if (company is null)
        {
            throw new InvalidOperationException("Selected company was not found.");
        }

        return company;
    }

    var mergeIntoAadwikaFashion = IsAadwikaAmitOrSmartProfile(profile);
    if (mergeIntoAadwikaFashion)
    {
        company = await db.Companies.FirstOrDefaultAsync(
            item => item.Code == "AF" || item.Name == "Aadwika Fashion",
            cancellationToken);
        if (company is not null)
        {
            return company;
        }
    }

    company = await db.Companies.FirstOrDefaultAsync(
        item => item.Code == profile.CompanyCode || item.Name == profile.CompanyName,
        cancellationToken);
    if (company is not null)
    {
        return company;
    }

    var companyCode = mergeIntoAadwikaFashion ? "AF" : profile.CompanyCode;
    company = new Company
    {
        Name = mergeIntoAadwikaFashion ? "Aadwika Fashion" : profile.CompanyName,
        Code = companyCode,
        Active = true,
        GSTIN = profile.Gstin,
        Pan = profile.Pan,
        ContactPerson = profile.ContactPerson,
        ContactNumber = profile.ContactNumber,
        ContactMobile = profile.ContactNumber,
        Email = profile.Email,
        Address = profile.StoreName.Contains("Smart", StringComparison.OrdinalIgnoreCase) ? "Bhagalpur Road, Dumka" : "Ground Floor, Bhagalpur Road, Dumka",
        City = profile.City,
        State = profile.State,
        Country = "India",
        ZipCode = profile.ZipCode,
        StartDate = DateTime.Today,
        CompanyType = CompanyType.Proprietorship,
        StoreCategory = StoreCategory.Cloths,
    };
    db.Companies.Add(company);
    return company;
}

private static bool IsAadwikaAmitOrSmartProfile(AfssSeedProfileDto profile)
    => profile.Code.Equals("AF", StringComparison.OrdinalIgnoreCase)
       || profile.Code.Equals("SM", StringComparison.OrdinalIgnoreCase);

public async Task<AfssSeedCreatedCountsDto> SeedAccountingDefaultsForCompanyAsync(Guid companyId, CancellationToken cancellationToken)
{
    var company = await db.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken)
        ?? throw new InvalidOperationException("Company was not found.");
    var counters = new AfssSeedCounters();
    await EnsureBanksAsync(counters, cancellationToken);
    await EnsureTaxesAsync(counters, cancellationToken);
    await EnsureTransactionsAsync(company, counters, cancellationToken);
    var ledgerContext = await EnsureLedgerGroupsAndLedgersAsync(company, counters, cancellationToken);
    await EnsureSbiCurrentAccountAsync(company, ledgerContext.BankGroup, counters, cancellationToken);
    await db.SaveChangesAsync(cancellationToken);
    return counters.ToCreatedDto();
}

    private static void PatchCompanyDefaults(Company company, AfssSeedProfileDto profile)
    {
        if (string.IsNullOrWhiteSpace(company.Code)) company.Code = profile.CompanyCode;
        if (string.IsNullOrWhiteSpace(company.GSTIN)) company.GSTIN = profile.Gstin;
        if (string.IsNullOrWhiteSpace(company.Pan)) company.Pan = profile.Pan;
        if (string.IsNullOrWhiteSpace(company.ContactPerson)) company.ContactPerson = profile.ContactPerson;
        if (string.IsNullOrWhiteSpace(company.ContactNumber)) company.ContactNumber = profile.ContactNumber;
        if (string.IsNullOrWhiteSpace(company.ContactMobile)) company.ContactMobile = profile.ContactNumber;
        if (string.IsNullOrWhiteSpace(company.Email)) company.Email = profile.Email;
        if (string.IsNullOrWhiteSpace(company.City)) company.City = profile.City;
        if (string.IsNullOrWhiteSpace(company.State)) company.State = profile.State;
        if (string.IsNullOrWhiteSpace(company.Country)) company.Country = "India";
        if (string.IsNullOrWhiteSpace(company.ZipCode)) company.ZipCode = profile.ZipCode;
        if (string.IsNullOrWhiteSpace(company.Address)) company.Address = profile.StoreName.Contains("Smart", StringComparison.OrdinalIgnoreCase)
            ? "Bhagalpur Road, Dumka"
            : "Ground Floor, Bhagalpur Road, Dumka";
        if (string.IsNullOrWhiteSpace(company.CIN)) company.CIN = profile.Code == "AF" ? "33AABCA1234A1Z5" : "NA";
        company.Active = true;
        company.CompanyType = CompanyType.Proprietorship;
        company.StoreCategory = StoreCategory.Cloths;
    }

    private async Task<StoreGroup> EnsureStoreGroupAsync(Company company, AfssSeedProfileDto profile, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var storeGroup = await db.StoreGroups.FirstOrDefaultAsync(
            item => item.CompanyId == company.Id && item.GroupCode == profile.StoreGroupCode,
            cancellationToken)
            ?? await db.StoreGroups.FirstOrDefaultAsync(item => item.CompanyId == company.Id, cancellationToken);

        if (storeGroup is not null)
        {
            return storeGroup;
        }

        storeGroup = new StoreGroup
        {
            Name = profile.StoreGroupName,
            GroupCode = profile.StoreGroupCode,
            Active = true,
            CompanyId = company.Id,
            StoreCategory = StoreCategory.Cloths,
            StartDate = company.StartDate == default ? DateTime.Today : company.StartDate
        };
        db.StoreGroups.Add(storeGroup);
        counters.StoreGroups++;
        return storeGroup;
    }

    private async Task<Store> EnsureStoreAsync(Company company, StoreGroup storeGroup, AfssSeedProfileDto profile, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var store = await db.Stores.FirstOrDefaultAsync(
            item => item.CompanyId == company.Id && item.StoreGroupId == storeGroup.Id && item.StoreCode == profile.StoreCode,
            cancellationToken);

        if (store is not null)
        {
            return store;
        }

        store = new Store
        {
            Name = profile.StoreName,
            StoreCode = profile.StoreCode,
            Active = true,
            CompanyId = company.Id,
            StoreGroupId = storeGroup.Id,
            StoreCategory = StoreCategory.Cloths,
            StartDate = company.StartDate == default ? DateTime.Today : company.StartDate,
            ContactNumber = profile.ContactNumber,
            Email = profile.Email,
            Address = profile.StoreName.Contains("Smart", StringComparison.OrdinalIgnoreCase) ? "Bhagalpur Road, Dumka" : "Ground Floor, Bhagalpur Road, Dumka",
            City = profile.City,
            State = profile.State,
            Country = "India",
            ZipCode = profile.ZipCode
        };
        db.Stores.Add(store);
        counters.Stores++;
        return store;
    }

    private async Task EnsureBanksAsync(AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        foreach (var bankName in new[] { "State Bank Of India", "ICICI Bank", "HDFC Bank", "Bank of Baroda", "Kotka Bank", "Punjab National Bank" })
        {
            if (await db.Banks.AnyAsync(item => item.Name == bankName, cancellationToken))
            {
                continue;
            }

            db.Banks.Add(new Bank { Name = bankName });
            counters.Banks++;
        }
    }

    private async Task EnsureTaxesAsync(AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        await EnsureTaxAsync("GST 5%", 5, TaxType.GST, counters, cancellationToken);
        await EnsureTaxAsync("GST 12%", 12, TaxType.GST, counters, cancellationToken);
        await EnsureTaxAsync("GST 18%", 18, TaxType.GST, counters, cancellationToken);
        await EnsureTaxAsync("IGST 5%", 5, TaxType.IGST, counters, cancellationToken);
        await EnsureTaxAsync("IGST 12%", 12, TaxType.IGST, counters, cancellationToken);
        await EnsureTaxAsync("IGST 18%", 18, TaxType.IGST, counters, cancellationToken);
        await EnsureTaxAsync("CGST 2.5%", 5, TaxType.GST, counters, cancellationToken);
        await EnsureTaxAsync("SGST 2.5%", 12, TaxType.GST, counters, cancellationToken);
    }

    private async Task<Tax> EnsureTaxAsync(string name, decimal compositeRate, TaxType taxType, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var tax = await db.Taxes.FirstOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (tax is not null)
        {
            return tax;
        }

        tax = new Tax { Name = name, CompositeRate = compositeRate, TaxType = taxType };
        db.Taxes.Add(tax);
        counters.Taxes++;
        return tax;
    }

    private async Task EnsureTransactionsAsync(Company company, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        foreach (var name in new[] { "Petty Cash Expenses", "Home Expenses", "Store Expenses", "Dan & Donations", "Snacks & Breakfast Expenses", "Cash In", "Cash Out" })
        {
            if (await db.Transactions.AnyAsync(item => item.CompanyId == company.Id && item.Name == name, cancellationToken))
            {
                continue;
            }

            db.Transactions.Add(new Transaction { CompanyId = company.Id, Name = name });
            counters.Transactions++;
        }
    }

    private async Task<LedgerSeedContext> EnsureLedgerGroupsAndLedgersAsync(Company company, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var groups = new Dictionary<string, LedgerGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in LedgerGroupDefinitions())
        {
            groups[definition.Name] = await EnsureLedgerGroupAsync(company, definition.Name, definition.Category, definition.Remarks, counters, cancellationToken);
        }

        foreach (var definition in LedgerDefinitions())
        {
            var ledgerGroup = groups[definition.GroupName];
            var existingLedger = await db.Ledgers.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Name == definition.Name, cancellationToken);
            if (existingLedger is not null)
            {
                if (string.IsNullOrWhiteSpace(existingLedger.CreatedBy))
                {
                    existingLedger.CreatedBy = AccountingDefaultProtection.CreatedByMarker;
                }
                continue;
            }

            db.Ledgers.Add(new Ledger
            {
                CompanyId = company.Id,
                Name = definition.Name,
                LedgerGroupId = ledgerGroup.Id,
                LedgerType = definition.LedgerType,
                OpeningBalance = 0,
                OpeningDate = company.StartDate == default ? DateTime.Today : company.StartDate,
                IsParty = false,
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            });
            counters.Ledgers++;
        }

        return new LedgerSeedContext(groups["Banks"], groups["Sales"], groups["Purchases"], groups["Cash"]);
    }

    private async Task<LedgerGroup> EnsureLedgerGroupAsync(Company company, string name, LedgerCategory category, string remarks, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var ledgerGroup = await db.LedgerGroups.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Name == name, cancellationToken);
        if (ledgerGroup is not null)
        {
            if (string.IsNullOrWhiteSpace(ledgerGroup.CreatedBy))
            {
                ledgerGroup.CreatedBy = AccountingDefaultProtection.CreatedByMarker;
            }
            return ledgerGroup;
        }

        ledgerGroup = new LedgerGroup
        {
            CompanyId = company.Id,
            Name = name,
            Category = category,
            Remarks = remarks,
            CreatedBy = AccountingDefaultProtection.CreatedByMarker
        };
        db.LedgerGroups.Add(ledgerGroup);
        counters.LedgerGroups++;
        return ledgerGroup;
    }

    private async Task EnsureSbiCurrentAccountAsync(Company company, LedgerGroup bankGroup, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var sbi = await db.Banks.FirstOrDefaultAsync(item => item.Name == "State Bank Of India", cancellationToken);
        if (sbi is null)
        {
            return;
        }

        var ledger = await db.Ledgers.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Name == "State Bank of India (SBI) Current Account", cancellationToken);
        if (ledger is null)
        {
            ledger = new Ledger
            {
                CompanyId = company.Id,
                Name = "State Bank of India (SBI) Current Account",
                LedgerGroupId = bankGroup.Id,
                LedgerType = LedgerType.BankAccount,
                OpeningBalance = 0,
                OpeningDate = company.StartDate == default ? DateTime.Today : company.StartDate,
                IsParty = false,
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            };
            db.Ledgers.Add(ledger);
            counters.Ledgers++;
        }

        if (await db.BankAccounts.AnyAsync(item => item.CompanyId == company.Id && item.LedgerId == ledger.Id, cancellationToken))
        {
            return;
        }

        db.BankAccounts.Add(new BankAccount
        {
            CompanyId = company.Id,
            AccountHolderName = company.Name,
            AccountNumber = "SBI Current Account",
            AccountType = AccountType.Current,
            Active = true,
            BankId = sbi.Id,
            Branch = company.City,
            IFSCode = "NA",
            LedgerId = ledger.Id,
            OpeningBalance = 0,
            ClosingBalance = 0,
            OpeningDate = company.StartDate == default ? DateTime.Today : company.StartDate
        });
        counters.BankAccounts++;
    }

    private async Task EnsureEmployeesAndSalesmanAsync(Company company, StoreGroup storeGroup, Store store, AfssSeedProfileDto profile, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        await EnsureEmployeeAsync(company, storeGroup, store, profile.ContactPerson, EmployeeCategory.Owner, 1, profile.ContactNumber, profile.Email, profile.Code == "AFS" ? Gender.Female : Gender.Male, counters, cancellationToken);
        var manager = await EnsureEmployeeAsync(company, storeGroup, store, "Alok Kumar", EmployeeCategory.StoreManager, 2, "1234567890", "alok@aadwikafashion.in", Gender.Male, counters, cancellationToken);
        await EnsureEmployeeAsync(company, storeGroup, store, "Accountant Manager", EmployeeCategory.Accounts, 3, "9876543210", "accountant@aadwikafashion.in", Gender.Male, counters, cancellationToken);

        if (!await db.Salesmen.AnyAsync(item => item.CompanyId == company.Id && item.StoreId == store.Id && item.Name == "Manager", cancellationToken))
        {
            db.Salesmen.Add(new Salesman
            {
                Name = "Manager",
                Active = true,
                EmployeeId = manager.Id,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                StoreId = store.Id
            });
            counters.Salesmen++;
        }
    }

    private async Task<Employee> EnsureEmployeeAsync(Company company, StoreGroup storeGroup, Store store, string fullName, EmployeeCategory category, int empId, string mobile, string email, Gender gender, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.StoreId == store.Id && item.EmpId == empId, cancellationToken)
            ?? await db.Employees.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.StoreId == store.Id && item.Mobile == mobile, cancellationToken);

        if (employee is not null)
        {
            return employee;
        }

        var nameParts = SplitName(fullName);
        employee = new Employee
        {
            Title = gender == Gender.Female ? "Mrs." : "Mr.",
            FirstName = nameParts.First,
            LastName = nameParts.Last,
            Gender = gender,
            DateOfBirth = DateTime.Today.AddYears(-25),
            EmpId = empId,
            JoiningDate = store.StartDate == default ? DateTime.Today : store.StartDate,
            Working = true,
            Category = category,
            PAN = "NA",
            Aadhar = "000000000000",
            Email = email,
            Mobile = mobile,
            CompanyId = company.Id,
            StoreGroupId = storeGroup.Id,
            StoreId = store.Id,
            CreatedBy = AccountingDefaultProtection.CreatedByMarker
        };
        db.Employees.Add(employee);
        counters.Employees++;
        return employee;
    }

    private async Task EnsureUsersAsync(Company company, StoreGroup storeGroup, Store store, AfssSeedProfileDto profile, bool resetPasswords, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var prefix = profile.Code.ToUpperInvariant();
        await EnsureUserAsync($"{prefix}Admin", "Admin", $"admin@{profile.BaseCompanyUrl}", "Admin@1234", LoginRole.Admin, UserType.Admin, AppOperation.Company, true, company.Id, storeGroup.Id, store.Id, resetPasswords, counters, cancellationToken);
        await EnsureUserAsync($"{prefix}Owner", profile.ContactPerson, store.Email, "Owner@1234", LoginRole.Member, UserType.Owner, AppOperation.All, true, company.Id, storeGroup.Id, store.Id, resetPasswords, counters, cancellationToken);
        await EnsureUserAsync($"{prefix}StoreManager", "Store Manager", $"storemanager@{profile.BaseCompanyUrl}", "StoreManager@1234", LoginRole.StoreManager, UserType.StoreManager, AppOperation.Store, false, company.Id, storeGroup.Id, store.Id, resetPasswords, counters, cancellationToken);
    }

    private async Task EnsureUserAsync(string userName, string name, string email, string password, LoginRole role, UserType userType, AppOperation appOperation, bool admin, Guid companyId, Guid storeGroupId, Guid storeId, bool resetPassword, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.UserName == userName, cancellationToken);

        if (user is not null)
        {
            user.Name = string.IsNullOrWhiteSpace(user.Name) ? name : user.Name;
            user.Email = string.IsNullOrWhiteSpace(user.Email) ? email : user.Email;
            user.CompanyId ??= companyId;
            user.StoreGroupId ??= storeGroupId;
            user.StoreId ??= storeId;
            if (resetPassword || string.IsNullOrWhiteSpace(user.Password) || PasswordHasher.NeedsUpgrade(user.Password))
            {
                user.Password = PasswordHasher.Hash(password);
            }
            return;
        }

        db.Users.Add(new AppUser
        {
            Name = name,
            UserName = userName,
            Email = email,
            Password = PasswordHasher.Hash(password),
            PinHash = "1234",
            Role = role,
            UserType = userType,
            Admin = admin || role == LoginRole.Admin,
            AppOperation = appOperation,
            CompanyId = companyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        });
        counters.Users++;
    }

    private async Task EnsureProductMastersAsync(Company company, StoreGroup storeGroup, Store store, AfssSeedProfileDto profile, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var vendor = await EnsureVendorAsync(company, profile, counters, cancellationToken);
        var brand = await EnsureBrandAsync(profile.Code, profile.StoreName, vendor.Id, counters, cancellationToken);
        var tax5 = await EnsureTaxAsync("GST 5%", 5, TaxType.GST, counters, cancellationToken);
        var tax12 = await EnsureTaxAsync("GST 12%", 12, TaxType.GST, counters, cancellationToken);

        var shirting = await EnsureCategoryAsync(company, "Shirting", ProductGroup.Shirting, counters, cancellationToken);
        var suiting = await EnsureCategoryAsync(company, "Suiting", ProductGroup.Suiting, counters, cancellationToken);
        var readymade = await EnsureCategoryAsync(company, "Readymade", ProductGroup.Readymade, counters, cancellationToken);
        var shoes = await EnsureCategoryAsync(company, "Shoes", ProductGroup.Shoes, counters, cancellationToken);

        var shirts = await EnsureSubCategoryAsync(company, shirting, "Cotton Shirting", counters, cancellationToken);
        var suitings = await EnsureSubCategoryAsync(company, suiting, "Suiting Fabric", counters, cancellationToken);
        var ethnic = await EnsureSubCategoryAsync(company, readymade, "Ethnic Readymade", counters, cancellationToken);
        var footwear = await EnsureSubCategoryAsync(company, shoes, "Ethnic Footwear", counters, cancellationToken);

        var prefix = string.IsNullOrWhiteSpace(company.Code) ? profile.Code : company.Code;
        await EnsureProductAsync(company, storeGroup, store, vendor, brand.Name, shirting, shirts, tax5, $"{prefix}-SHIRTING-001", "Cotton Shirting Fabric", "5208", Unit.Meters, ProductType.Fabric, ProductGroup.Shirting, 499m, 100m, 260m, "SH-001", "White", counters, cancellationToken);
        await EnsureProductAsync(company, storeGroup, store, vendor, brand.Name, suiting, suitings, tax5, $"{prefix}-SUITING-001", "Premium Suiting Fabric", "5515", Unit.Meters, ProductType.Fabric, ProductGroup.Suiting, 899m, 80m, 520m, "SU-001", "Navy", counters, cancellationToken);
        await EnsureProductAsync(company, storeGroup, store, vendor, brand.Name, readymade, ethnic, tax12, $"{prefix}-KURTA-001", "Readymade Kurta Pajama", "6205", Unit.Pcs, ProductType.Readymade, ProductGroup.KurtaPajama, 1499m, 25m, 850m, "KP-001", "Cream", counters, cancellationToken);
        await EnsureProductAsync(company, storeGroup, store, vendor, brand.Name, shoes, footwear, tax12, $"{prefix}-NAGRA-001", "Ethnic Nagra Shoes", "6403", Unit.Pcs, ProductType.Shoes, ProductGroup.Nagra, 1299m, 15m, 700m, "NG-001", "Brown", counters, cancellationToken);
    }

    private async Task<Vendor> EnsureVendorAsync(Company company, AfssSeedProfileDto profile, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.MobileNumber == profile.ContactNumber && item.Name == "Default Supplier", cancellationToken);
        if (vendor is not null)
        {
            return vendor;
        }

        vendor = new Vendor
        {
            Name = "Default Supplier",
            Address = "Supplier Market, Dumka",
            City = profile.City,
            ZipCode = profile.ZipCode,
            MobileNumber = profile.ContactNumber,
            Email = profile.Email,
            GSTIN = profile.Gstin,
            Pan = profile.Pan,
            Active = true,
            CompanyId = company.Id
        };
        db.Vendors.Add(vendor);
        counters.Vendors++;
        return vendor;
    }

    private async Task<Brand> EnsureBrandAsync(string code, string storeName, Guid supplierId, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var brandCode = $"{code}-DEFAULT";
        var brand = await db.Brands.FirstOrDefaultAsync(item => item.BrandCode == brandCode, cancellationToken);
        if (brand is not null)
        {
            return brand;
        }

        brand = new Brand
        {
            Name = storeName,
            BrandCode = brandCode,
            SupplierId = supplierId
        };
        db.Brands.Add(brand);
        counters.Brands++;
        return brand;
    }

    private async Task<InventoryProductCategory> EnsureCategoryAsync(Company company, string name, ProductGroup group, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var category = await db.ProductCategories.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Name == name, cancellationToken);
        if (category is not null)
        {
            return category;
        }

        category = new InventoryProductCategory
        {
            CompanyId = company.Id,
            Name = name,
            ProductGroup = group,
            IsActive = true,
            CreatedBy = AccountingDefaultProtection.CreatedByMarker
        };
        db.ProductCategories.Add(category);
        counters.ProductCategories++;
        return category;
    }

    private async Task<InventoryProductSubCategory> EnsureSubCategoryAsync(Company company, InventoryProductCategory category, string name, AfssSeedCounters counters, CancellationToken cancellationToken)
    {
        var subCategory = await db.ProductSubCategories.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.CategoryId == category.Id && item.Name == name, cancellationToken);
        if (subCategory is not null)
        {
            return subCategory;
        }

        subCategory = new InventoryProductSubCategory
        {
            CompanyId = company.Id,
            CategoryId = category.Id,
            Name = name,
            CreatedBy = AccountingDefaultProtection.CreatedByMarker
        };
        db.ProductSubCategories.Add(subCategory);
        counters.ProductSubCategories++;
        return subCategory;
    }

    private async Task EnsureProductAsync(
        Company company,
        StoreGroup storeGroup,
        Store store,
        Vendor vendor,
        string brandName,
        InventoryProductCategory category,
        InventoryProductSubCategory subCategory,
        Tax tax,
        string barcode,
        string name,
        string hsn,
        Unit unit,
        ProductType productType,
        ProductGroup productGroup,
        decimal mrp,
        decimal openingQty,
        decimal costPrice,
        string styleCode,
        string baseColor,
        AfssSeedCounters counters,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Barcode == barcode, cancellationToken);
        if (product is null)
        {
            product = new Product
            {
                Name = name,
                Barcode = barcode,
                Descriptions = "Seeded from AF/SS default seeder.",
                HSNCode = hsn,
                MRP = mrp,
                TaxRate = tax.CompositeRate,
                Unit = unit,
                TaxType = tax.TaxType,
                ProductType = productType,
                ProductGroup = productGroup,
                ProductCategoryId = category.Id,
                ProductSubCategoryId = subCategory.Id,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            };
            db.Products.Add(product);
            counters.Products++;
        }

        var stock = await db.Stocks.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.StoreId == store.Id && item.Barcode == barcode, cancellationToken);
        if (stock is null)
        {
            stock = new Stock
            {
                ProductId = product.Id,
                Barcode = barcode,
                HSNCode = hsn,
                Unit = unit,
                PurchaseQty = openingQty,
                CostPrice = costPrice,
                SoldQty = 0,
                MRP = mrp,
                TaxRate = tax.CompositeRate,
                TaxType = tax.TaxType,
                TaxId = tax.Id,
                BrandedProduct = true,
                StockType = StockType.Opening,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                StoreId = store.Id,
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            };
            db.Stocks.Add(stock);
            counters.Stocks++;
        }

        if (!await db.StockMovements.AnyAsync(item => item.CompanyId == company.Id && item.StoreId == store.Id && item.SourceType == "AFSSSeed" && item.SourceNumber == barcode, cancellationToken))
        {
            db.StockMovements.Add(new StockMovement
            {
                StockId = stock.Id,
                ProductId = product.Id,
                Barcode = barcode,
                MovementType = "Opening",
                QuantityIn = openingQty,
                QuantityOut = 0,
                CostPrice = costPrice,
                MRP = mrp,
                TaxRate = tax.CompositeRate,
                HSNCode = hsn,
                SourceType = "AFSSSeed",
                SourceId = product.Id,
                SourceNumber = barcode,
                Remarks = "Opening stock created by AF/SS default seeder.",
                OnDate = DateTime.Now,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                StoreId = store.Id,
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            });
            counters.StockMovements++;
        }

        if (!await db.ProductDetails.AnyAsync(item => item.CompanyId == company.Id && item.ProductId == product.Id && item.Barcode == barcode, cancellationToken))
        {
            db.ProductDetails.Add(new ProductDetail
            {
                ProductId = product.Id,
                Barcode = barcode,
                StyleCode = styleCode,
                BaseColor = baseColor,
                Brand = brandName,
                VendorId = vendor.Id,
                CompanyId = company.Id,
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            });
            counters.ProductDetails++;
        }
    }

    private async Task<AfssSeedExistingCountsDto> BuildExistingCountsAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
    {
        return new AfssSeedExistingCountsDto(
            StoreGroups: await db.StoreGroups.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            Stores: await db.Stores.CountAsync(item => item.CompanyId == companyId && item.StoreGroupId == storeGroupId, cancellationToken),
            Banks: await db.Banks.CountAsync(cancellationToken),
            Taxes: await db.Taxes.CountAsync(cancellationToken),
            Transactions: await db.Transactions.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            LedgerGroups: await db.LedgerGroups.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            Ledgers: await db.Ledgers.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            BankAccounts: await db.BankAccounts.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            Employees: await db.Employees.CountAsync(item => item.CompanyId == companyId && item.StoreId == storeId, cancellationToken),
            Salesmen: await db.Salesmen.CountAsync(item => item.CompanyId == companyId && item.StoreId == storeId, cancellationToken),
            Users: await db.Users.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            ProductCategories: await db.ProductCategories.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            ProductSubCategories: await db.ProductSubCategories.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            Brands: await db.Brands.CountAsync(cancellationToken),
            Vendors: await db.Vendors.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            Products: await db.Products.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            Stocks: await db.Stocks.CountAsync(item => item.CompanyId == companyId && item.StoreId == storeId, cancellationToken),
            StockMovements: await db.StockMovements.CountAsync(item => item.CompanyId == companyId && item.StoreId == storeId, cancellationToken),
            ProductDetails: await db.ProductDetails.CountAsync(item => item.CompanyId == companyId, cancellationToken));
    }

    private static IEnumerable<LedgerGroupDefinition> LedgerGroupDefinitions()
    {
        yield return new("Snacks & Refeshments", LedgerCategory.Expenses, "Store Snacks & Refeshments Expenses");
        yield return new("Store Expenses", LedgerCategory.Expenses, "Store Expenses");
        yield return new("Direct Expenses", LedgerCategory.Expenses, "Store Direct Expenses");
        yield return new("Indirect Expenses", LedgerCategory.Expenses, "Store Indirect Expenses");
        yield return new("Petty Expenses", LedgerCategory.Expenses, "Store Petty Expenses");
        yield return new("No Group", LedgerCategory.UnCategory, "NO GROUP & Party, Group for UnCategory Ledger");
        yield return new("Sales", LedgerCategory.Income, "Store Sales");
        yield return new("Purchases", LedgerCategory.Purchase, "Store Purchase");
        yield return new("Cash", LedgerCategory.Assets, "Store Cash(s)");
        yield return new("Banks", LedgerCategory.Assets, "Store Bank");
        yield return new("Capital Accounts", LedgerCategory.Assets, "Store Capital Account");
        yield return new("Loans and Adavances", LedgerCategory.Assets, "Store Loan Account");
        yield return new("Vendors", LedgerCategory.Vendor, "Store Vendor Account");
        yield return new("Customers", LedgerCategory.Customer, "Store Customer Account");
        yield return new("Employees", LedgerCategory.Employees, "Store Employee Account");
        yield return new("Stock", LedgerCategory.Stock, "Store Stock Account");
        yield return new("Debitors", LedgerCategory.Debitor, "Store Debitor Account");
        yield return new("Creditors", LedgerCategory.Creditor, "Store Creditor Account");
    }

    private static IEnumerable<LedgerDefinition> LedgerDefinitions()
    {
        yield return new("Dan", "Petty Expenses", LedgerType.Expenses);
        yield return new("Snacks & Tea", "Snacks & Refeshments", LedgerType.Expenses);
        yield return new("Electricity", "Store Expenses", LedgerType.Expenses);
        yield return new("Water", "Snacks & Refeshments", LedgerType.Expenses);
        yield return new("Printing & Stationery", "Store Expenses", LedgerType.Expenses);
        yield return new("Transports & Freight Charges", "Direct Expenses", LedgerType.Expenses);
        yield return new("Miscellaneous", "Store Expenses", LedgerType.Expenses);
        yield return new("No Party", "No Group", LedgerType.IndirectExpenses);
        yield return new("Cash In Hand", "Cash", LedgerType.Cash);
        yield return new("Salary Payables", "Direct Expenses", LedgerType.Expenses);
        yield return new("Internet & Mobile Bills", "Store Expenses", LedgerType.Expenses);
        yield return new("Store Maintenance", "Store Expenses", LedgerType.Expenses);
        yield return new("Store Supplies", "Store Expenses", LedgerType.Expenses);
        yield return new("Petty Cash Expenses", "Petty Expenses", LedgerType.Expenses);
    }

    private static (string First, string Last) SplitName(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => ("Unknown", ""),
            1 => (parts[0], ""),
            _ => (parts[0], string.Join(' ', parts.Skip(1)))
        };
    }

    private sealed record LedgerSeedContext(LedgerGroup BankGroup, LedgerGroup SalesGroup, LedgerGroup PurchaseGroup, LedgerGroup CashGroup);
    private sealed record LedgerGroupDefinition(string Name, LedgerCategory Category, string Remarks);
    private sealed record LedgerDefinition(string Name, string GroupName, LedgerType LedgerType);
}
