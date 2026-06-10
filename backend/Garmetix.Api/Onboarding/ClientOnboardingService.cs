using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;

namespace Garmetix.Api.Onboarding;

public sealed class ClientOnboardingService(GarmetixDbContext db)
{
    public static readonly IReadOnlyList<string> FlowSteps =
    [
        "Step 1 — Client / owner login details",
        "Step 2 — Company statutory and contact details",
        "Step 3 — Address details",
        "Step 4 — Company, store-group and store codes",
        "Step 5 — Key personnel for store manager and accountant",
        "Step 6 — Review and create company structure"
    ];

    public static readonly IReadOnlyList<string> ModelMappingNotes =
    [
        "MAUI ClientInfo maps to owner AppUser plus Owner employee.",
        "MAUI CompanyInfo maps to Company with current CompanyType and StoreCategory enums.",
        "MAUI AddressInfo maps to Company and Store address fields.",
        "MAUI CompanyConfigInfo maps to Company.Code, StoreGroup.GroupCode, Store.StoreCode and AppUser.AppOperation.",
        "MAUI KeyPersonalInfo maps to StoreManager and Accountant Employee rows plus Manager Salesman row.",
        "New web model defaults use current BaseEntity dates, PBKDF2 password hashing, Salesmen DbSet, and current Tax/ProductCategory model shapes."
    ];

    public async Task<ClientOnboardingResponseDto> OnboardAsync(ClientOnboardingRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var companyCode = NormalizeCode(request.CompanyConfig.ClientCode, "Client code");
        var groupCode = NormalizeCode(request.CompanyConfig.GroupCode, "Group code");
        var storeCode = NormalizeCode(request.CompanyConfig.StoreCode, "Store code");
        var gstin = request.CompanyDetails.GSTIN.Trim().ToUpperInvariant();
        var pan = request.CompanyDetails.PAN.Trim().ToUpperInvariant();

        if (await db.Companies.AnyAsync(item => item.Code.ToUpper() == companyCode, cancellationToken))
        {
            throw new InvalidOperationException($"Company code {companyCode} already exists. Use a unique client code.");
        }

        if (!string.IsNullOrWhiteSpace(gstin) && await db.Companies.AnyAsync(item => item.GSTIN.ToUpper() == gstin, cancellationToken))
        {
            throw new InvalidOperationException($"Company GSTIN {gstin} already exists. Use a unique GSTIN.");
        }

        if (await db.Users.AnyAsync(item => item.Email.ToUpper() == request.ClientDetails.Email.Trim().ToUpper(), cancellationToken))
        {
            throw new InvalidOperationException($"A user already exists with email {request.ClientDetails.Email}.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var counters = new ClientOnboardingCounters();
        var notes = new List<string>();

        var ownerName = JoinName(request.ClientDetails.FirstName, request.ClientDetails.LastName);
        var companyStartDate = (request.CompanyDetails.DateOfIncorporation ?? DateTime.Today).Date;
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.CompanyDetails.CompanyName.Trim(),
            GSTIN = gstin,
            Pan = pan,
            City = request.AddressDetails.City.Trim(),
            Code = companyCode,
            CompanyType = request.CompanyDetails.CompanyType,
            ContactNumber = request.CompanyDetails.CompanyPhoneNumber.Trim(),
            Country = request.AddressDetails.Country.Trim(),
            StartDate = companyStartDate,
            StoreCategory = request.CompanyDetails.StoreCategory,
            State = request.AddressDetails.StateOrProvince.Trim(),
            ContactPerson = ownerName,
            ZipCode = request.AddressDetails.PostalCode.Trim(),
            Active = true,
            Address = request.AddressDetails.StreetAddress.Trim(),
            CIN = string.IsNullOrWhiteSpace(request.CompanyDetails.CIN) ? "NA" : request.CompanyDetails.CIN.Trim(),
            ContactMobile = request.ClientDetails.PhoneNumber.Trim(),
            Email = request.CompanyDetails.CompanyEmail.Trim()
        };

        var group = new StoreGroup
        {
            Id = Guid.NewGuid(),
            Active = true,
            CompanyId = company.Id,
            GroupCode = groupCode,
            Name = string.IsNullOrWhiteSpace(request.CompanyConfig.GroupName)
                ? $"{company.Name} Group"
                : request.CompanyConfig.GroupName.Trim(),
            StartDate = company.StartDate,
            StoreCategory = company.StoreCategory,
            Company = company
        };

        var store = new Store
        {
            Id = Guid.NewGuid(),
            Company = company,
            StoreGroup = group,
            CompanyId = company.Id,
            StoreGroupId = group.Id,
            StoreCode = storeCode,
            Name = string.IsNullOrWhiteSpace(request.CompanyConfig.StoreName)
                ? $"{company.Name} Main Store"
                : request.CompanyConfig.StoreName.Trim(),
            StartDate = company.StartDate,
            StoreCategory = company.StoreCategory,
            Country = company.Country,
            State = company.State,
            ZipCode = company.ZipCode,
            Active = true,
            Address = company.Address,
            City = company.City,
            ContactNumber = company.ContactNumber,
            Email = company.Email
        };

        db.Companies.Add(company);
        db.StoreGroups.Add(group);
        db.Stores.Add(store);
        counters.Companies++;
        counters.StoreGroups++;
        counters.Stores++;

        var ownerEmployee = AddEmployee(store, company, request.ClientDetails.FirstName, request.ClientDetails.LastName,
            request.ClientDetails.PhoneNumber, request.ClientDetails.Email, request.ClientDetails.Gender,
            request.ClientDetails.DateOfBirth ?? DateTime.Today.AddYears(-25), EmployeeCategory.Owner, 1, company.StartDate, counters);

        var managerParts = SplitName(request.KeyPersonalDetails.StoreManagerName);
        var managerEmployee = AddEmployee(store, company, managerParts.FirstName, managerParts.LastName,
            request.KeyPersonalDetails.StoreManagerPhoneNumber, request.KeyPersonalDetails.StoreManagerEmail, Gender.Male,
            DateTime.Today.AddYears(-25), EmployeeCategory.StoreManager, 2, company.StartDate, counters);

        var accountantParts = SplitName(request.KeyPersonalDetails.AccountantName);
        AddEmployee(store, company, accountantParts.FirstName, accountantParts.LastName,
            request.KeyPersonalDetails.AccountantPhoneNumber, request.KeyPersonalDetails.AccountantEmail, Gender.Male,
            DateTime.Today.AddYears(-25), EmployeeCategory.Accounts, 3, company.StartDate, counters);

        db.Salesmen.Add(new Salesman
        {
            Id = managerEmployee.Id,
            Name = "Manager",
            EmployeeId = managerEmployee.Id,
            CompanyId = company.Id,
            StoreGroupId = group.Id,
            StoreId = store.Id,
            Active = true
        });
        counters.Salesmen++;

        AddUser(company, group, store, $"{companyCode}Admin", "Admin", $"admin@{BuildBaseUrl(request, companyCode)}", request.ClientDetails.Password,
            LoginRole.Admin, UserType.Admin, AppOperation.Company, null, true, counters);
        AddUser(company, group, store, $"{companyCode}Owner", ownerName, request.ClientDetails.Email, request.ClientDetails.Password,
            LoginRole.Member, UserType.Owner, AppOperation.All, ownerEmployee.Id, true, counters);
        AddUser(company, group, store, $"{companyCode}StoreManager", "Store Manager", request.KeyPersonalDetails.StoreManagerEmail, "StoreManager@1234",
            LoginRole.StoreManager, UserType.StoreManager, AppOperation.Store, managerEmployee.Id, true, counters);
        AddUser(company, group, store, $"{companyCode}Accountant", "Accountant", request.KeyPersonalDetails.AccountantEmail, "Accountant@1234",
            LoginRole.Accountant, UserType.Accountant, AppOperation.Store, null, false, counters);

        if (request.SeedBasicStructure)
        {
            await SeedBasicStructureAsync(company, group, store, counters, notes, cancellationToken);
        }
        else
        {
            notes.Add("Basic masters were skipped by request. Only company/store/user structure was created.");
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = new ClientOnboardingResponseDto(
            $"Company {company.Name} onboarded successfully.",
            DateTimeOffset.UtcNow,
            new ClientOnboardingTargetDto(company.Id, company.Name, company.Code, group.Id, group.Name, group.GroupCode, store.Id, store.Name, store.StoreCode),
            counters.ToDto(),
            [
                $"Admin username: {companyCode}Admin",
                $"Owner username: {companyCode}Owner",
                $"Store manager username: {companyCode}StoreManager",
                $"Accountant username: {companyCode}Accountant"
            ],
            notes);

        return response;
    }

    private static void Validate(ClientOnboardingRequest request)
    {
        Require(request.ClientDetails.FirstName, "Client first name");
        Require(request.ClientDetails.LastName, "Client last name");
        Require(request.ClientDetails.Email, "Client email");
        Require(request.ClientDetails.Password, "Client password");
        if (request.ClientDetails.Password.Length < 8)
        {
            throw new InvalidOperationException("Client password must be at least 8 characters.");
        }

        Require(request.ClientDetails.PhoneNumber, "Client phone number");
        Require(request.CompanyDetails.CompanyName, "Company name");
        Require(request.CompanyDetails.GSTIN, "GSTIN");
        Require(request.CompanyDetails.PAN, "PAN");
        Require(request.CompanyDetails.CompanyEmail, "Company email");
        Require(request.CompanyDetails.CompanyPhoneNumber, "Company phone number");
        Require(request.AddressDetails.StreetAddress, "Street address");
        Require(request.AddressDetails.City, "City");
        Require(request.AddressDetails.StateOrProvince, "State");
        Require(request.AddressDetails.PostalCode, "Postal code");
        Require(request.AddressDetails.Country, "Country");
        Require(request.CompanyConfig.ClientCode, "Client code");
        Require(request.CompanyConfig.GroupCode, "Group code");
        Require(request.CompanyConfig.StoreCode, "Store code");
        Require(request.KeyPersonalDetails.StoreManagerName, "Store manager name");
        Require(request.KeyPersonalDetails.StoreManagerEmail, "Store manager email");
        Require(request.KeyPersonalDetails.StoreManagerPhoneNumber, "Store manager phone number");
        Require(request.KeyPersonalDetails.AccountantName, "Accountant name");
        Require(request.KeyPersonalDetails.AccountantEmail, "Accountant email");
        Require(request.KeyPersonalDetails.AccountantPhoneNumber, "Accountant phone number");

        if (!request.IsTermsAccepted || !request.IsPrivacyPolicyAccepted)
        {
            throw new InvalidOperationException("Terms and privacy policy must be accepted before onboarding.");
        }
    }

    private static void Require(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} is required.");
        }
    }

    private static string NormalizeCode(string value, string label)
    {
        Require(value, label);
        var code = new string(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (code.Length is < 2 or > 8)
        {
            throw new InvalidOperationException($"{label} must contain 2 to 8 letters/numbers.");
        }

        return code;
    }

    private static string BuildBaseUrl(ClientOnboardingRequest request, string companyCode)
        => string.IsNullOrWhiteSpace(request.CompanyConfig.BaseCompanyUrl)
            ? $"{companyCode.ToLowerInvariant()}.garmetix.local"
            : request.CompanyConfig.BaseCompanyUrl.Trim().TrimStart('@');

    private static string JoinName(string firstName, string lastName)
        => string.Join(' ', new[] { firstName.Trim(), lastName.Trim() }.Where(item => !string.IsNullOrWhiteSpace(item)));

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return ("Staff", string.Empty);
        }

        return (parts[0], parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty);
    }

    private Employee AddEmployee(Store store, Company company, string firstName, string lastName, string mobile, string email,
        Gender gender, DateTime dateOfBirth, EmployeeCategory category, int empId, DateTime joiningDate, ClientOnboardingCounters counters)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            StoreGroupId = store.StoreGroupId,
            StoreId = store.Id,
            Title = gender == Gender.Female ? "Mrs." : "Mr.",
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Mobile = mobile.Trim(),
            Email = email.Trim(),
            Gender = gender,
            DateOfBirth = dateOfBirth.Date,
            EmpId = empId,
            Category = category,
            JoiningDate = joiningDate.Date,
            LeavingDate = null,
            Working = true,
            PAN = "NA",
            Aadhar = "000000000000",
            CreatedBy = "ClientOnboarding"
        };

        db.Employees.Add(employee);
        counters.Employees++;
        return employee;
    }

    private void AddUser(Company company, StoreGroup group, Store store, string username, string name, string email, string password,
        LoginRole role, UserType userType, AppOperation operation, Guid? employeeId, bool admin, ClientOnboardingCounters counters)
    {
        db.Users.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserName = username,
            Email = email.Trim(),
            Password = PasswordHasher.Hash(password),
            PinHash = "1234",
            Role = role,
            UserType = userType,
            RemoteUserId = null,
            EmployeeId = employeeId,
            CompanyId = company.Id,
            StoreGroupId = group.Id,
            StoreId = store.Id,
            Admin = admin,
            AppOperation = operation
        });
        counters.Users++;
    }

    private async Task SeedBasicStructureAsync(Company company, StoreGroup group, Store store, ClientOnboardingCounters counters, List<string> notes, CancellationToken cancellationToken)
    {
        await AddBanksAsync(counters, cancellationToken);
        AddTaxes(company, counters);
        AddTransactions(company, counters);
        var bankLedgerGroup = AddLedgerGroupsAndLedgers(company, counters);
        await AddPrimaryBankAccountAsync(company, bankLedgerGroup, counters, cancellationToken);
        AddInventoryMasters(company, counters);
        notes.Add("Basic banks, taxes, transactions, ledger groups, ledgers, one bank account and product category masters were seeded.");
        notes.Add("No opening product stock is created here. Use AF/SS seeder or Product Master for product stock defaults.");
    }

    private async Task AddBanksAsync(ClientOnboardingCounters counters, CancellationToken cancellationToken)
    {
        foreach (var bankName in new[] { "State Bank Of India", "ICICI Bank", "HDFC Bank", "Bank of Baroda", "Kotak Bank", "Punjab National Bank" })
        {
            if (await db.Banks.AnyAsync(item => item.Name == bankName, cancellationToken))
            {
                continue;
            }

            db.Banks.Add(new Bank { Id = Guid.NewGuid(), Name = bankName });
            counters.Banks++;
        }
    }

    private void AddTaxes(Company company, ClientOnboardingCounters counters)
    {
        foreach (var tax in new[]
        {
            ("GST 5%", 5m, TaxType.GST),
            ("GST 12%", 12m, TaxType.GST),
            ("GST 18%", 18m, TaxType.GST),
            ("IGST 5%", 5m, TaxType.IGST),
            ("IGST 12%", 12m, TaxType.IGST),
            ("IGST 18%", 18m, TaxType.IGST)
        })
        {
            db.Taxes.Add(new Tax
            {
                Id = Guid.NewGuid(),
                Name = tax.Item1,
                CompositeRate = tax.Item2,
                TaxType = tax.Item3
            });
            counters.Taxes++;
        }
    }

    private void AddTransactions(Company company, ClientOnboardingCounters counters)
    {
        foreach (var name in new[] { "Petty Cash Expenses", "Home Expenses", "Store Expenses", "Dan & Donations", "Snacks & Breakfast Expenses", "Cash In", "Cash Out" })
        {
            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Name = name,
                CreatedBy = "ClientOnboarding"
            });
            counters.Transactions++;
        }
    }

    private LedgerGroup AddLedgerGroupsAndLedgers(Company company, ClientOnboardingCounters counters)
    {
        var groups = new[]
        {
            ("Cash", LedgerCategory.Assets, "Store cash accounts"),
            ("Banks", LedgerCategory.Assets, "Store bank accounts"),
            ("Sales", LedgerCategory.Income, "Sales ledgers"),
            ("Purchases", LedgerCategory.Purchase, "Purchase ledgers"),
            ("Store Expenses", LedgerCategory.Expenses, "Store expenses"),
            ("Petty Expenses", LedgerCategory.Expenses, "Petty cash expenses"),
            ("Customers", LedgerCategory.Customer, "Customer accounts"),
            ("Vendors", LedgerCategory.Vendor, "Vendor accounts"),
            ("Employees", LedgerCategory.Employees, "Employee accounts"),
            ("Stock", LedgerCategory.Stock, "Stock accounts"),
            ("Duties & Taxes", LedgerCategory.DutiesAndTaxes, "GST and tax ledgers"),
            ("No Group", LedgerCategory.UnCategory, "Fallback ledger group")
        };

        var createdGroups = new Dictionary<string, LedgerGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in groups)
        {
            var group = new LedgerGroup
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Name = item.Item1,
                Category = item.Item2,
                Remarks = item.Item3,
                CreatedBy = "ClientOnboarding"
            };
            db.LedgerGroups.Add(group);
            createdGroups[item.Item1] = group;
            counters.LedgerGroups++;
        }

        AddLedger(company, createdGroups["Cash"], "Cash In Hand", LedgerType.Cash, counters);
        AddLedger(company, createdGroups["Banks"], "State Bank of India (SBI) Current Account", LedgerType.BankAccount, counters);
        AddLedger(company, createdGroups["Store Expenses"], "Electricity", LedgerType.Expenses, counters);
        AddLedger(company, createdGroups["Store Expenses"], "Printing & Stationery", LedgerType.Expenses, counters);
        AddLedger(company, createdGroups["Store Expenses"], "Internet & Mobile Bills", LedgerType.Expenses, counters);
        AddLedger(company, createdGroups["Store Expenses"], "Store Maintenance", LedgerType.Expenses, counters);
        AddLedger(company, createdGroups["Petty Expenses"], "Petty Cash Expenses", LedgerType.Expenses, counters);
        AddLedger(company, createdGroups["No Group"], "No Party", LedgerType.IndirectExpenses, counters);
        AddLedger(company, createdGroups["Sales"], "Retail Sales", LedgerType.Income, counters);
        AddLedger(company, createdGroups["Purchases"], "Purchase Account", LedgerType.Expenses, counters);

        return createdGroups["Banks"];
    }

    private void AddLedger(Company company, LedgerGroup ledgerGroup, string name, LedgerType type, ClientOnboardingCounters counters)
    {
        db.Ledgers.Add(new Ledger
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            LedgerGroupId = ledgerGroup.Id,
            Name = name,
            LedgerType = type,
            OpeningDate = company.StartDate,
            OpeningBalance = 0,
            IsParty = false,
            CreatedBy = "ClientOnboarding"
        });
        counters.Ledgers++;
    }

    private async Task AddPrimaryBankAccountAsync(Company company, LedgerGroup bankGroup, ClientOnboardingCounters counters, CancellationToken cancellationToken)
    {
        var bank = await db.Banks.FirstOrDefaultAsync(item => item.Name == "State Bank Of India", cancellationToken);
        if (bank is null)
        {
            return;
        }

        var ledger = new Ledger
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            LedgerGroupId = bankGroup.Id,
            Name = $"{company.Code} Primary Bank Account",
            LedgerType = LedgerType.BankAccount,
            OpeningDate = company.StartDate,
            OpeningBalance = 0,
            IsParty = false,
            CreatedBy = "ClientOnboarding"
        };
        db.Ledgers.Add(ledger);
        counters.Ledgers++;

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BankId = bank.Id,
            LedgerId = ledger.Id,
            AccountHolderName = company.Name,
            AccountNumber = $"{company.Code} Primary Account",
            AccountType = AccountType.Current,
            Branch = company.City,
            IFSCode = "NA",
            OpeningDate = company.StartDate,
            OpeningBalance = 0,
            ClosingBalance = 0,
            Active = true,
            CreatedBy = "ClientOnboarding"
        });
        counters.BankAccounts++;
    }

    private void AddInventoryMasters(Company company, ClientOnboardingCounters counters)
    {
        foreach (var item in new[]
        {
            ("Shirting", ProductGroup.Shirting),
            ("Suiting", ProductGroup.Suiting),
            ("Readymade", ProductGroup.Readymade),
            ("Accessories", ProductGroup.Accessories)
        })
        {
            var category = new ProductCategory
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Name = item.Item1,
                ProductGroup = item.Item2,
                IsActive = true,
                CreatedBy = "ClientOnboarding"
            };
            db.ProductCategories.Add(category);
            counters.ProductCategories++;

            db.ProductSubCategories.Add(new ProductSubCategory
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Name = item.Item1 == "Readymade" ? "Menswear" : "General",
                CategoryId = category.Id,
                CreatedBy = "ClientOnboarding"
            });
            counters.ProductSubCategories++;
        }

        foreach (var brand in new[] { "Generic", company.Name })
        {
            db.Brands.Add(new Brand
            {
                Id = Guid.NewGuid(),
                Name = brand,
                BrandCode = NormalizeBrandCode(brand),
                SupplierId = null
            });
            counters.Brands++;
        }
    }

    private static string NormalizeBrandCode(string brand)
    {
        var code = new string(brand.ToUpperInvariant().Where(char.IsLetterOrDigit).Take(8).ToArray());
        return string.IsNullOrWhiteSpace(code) ? "BRAND" : code;
    }
}
