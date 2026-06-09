using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
using InventoryProductSubCategory = Garmetix.Core.Models.Inventory.ProductSubCategory;

namespace Garmetix.Api.Release;

public static class ReleaseStabilizationEndpoints
{
    public static RouteGroupBuilder MapReleaseStabilizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/release-stabilization")
            .WithTags("Release Stabilization")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/smoke-checks", GetSmokeChecksAsync);
        group.MapPost("/demo-data/seed", SeedDemoDataAsync);

        return group;
    }

    private static async Task<IResult> GetSmokeChecksAsync(
        GarmetixDbContext db,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var checks = new List<ReleaseSmokeCheckDto>();

        try
        {
            var databaseReady = await db.Database.CanConnectAsync(cancellationToken);
            Add(checks,
                "DATABASE_CONNECT",
                "Database connection",
                databaseReady ? "Pass" : "Critical",
                databaseReady ? "Info" : "High",
                databaseReady ? "Application can connect to PostgreSQL." : "Application could not connect to PostgreSQL.",
                "Check database container, connection string, user, password, and network.");
        }
        catch (Exception ex)
        {
            Add(checks,
                "DATABASE_CONNECT",
                "Database connection",
                "Critical",
                "High",
                $"Database connection failed: {ex.Message}",
                "Check PostgreSQL status and connection-string secrets.");
        }

        await AddCountCheckAsync(checks, "ADMIN_USER", "Admin user", await db.Users.CountAsync(user => user.Admin || user.Role == LoginRole.Admin, cancellationToken), 1, "At least one admin user exists.", "Create the first admin from bootstrap screen.");
        await AddCountCheckAsync(checks, "COMPANY", "Company master", await db.Companies.CountAsync(cancellationToken), 1, "Company master exists.", "Run Setup → Quick Start.");
        await AddCountCheckAsync(checks, "STORE_GROUP", "Store group master", await db.StoreGroups.CountAsync(cancellationToken), 1, "Store group master exists.", "Run Setup → Quick Start.");
        await AddCountCheckAsync(checks, "STORE", "Store master", await db.Stores.CountAsync(cancellationToken), 1, "Store master exists.", "Run Setup → Quick Start.");
        await AddCountCheckAsync(checks, "TAX", "Tax master", await db.Taxes.CountAsync(cancellationToken), 1, "Tax master exists.", "Create GST tax rates before billing.");
        await AddCountCheckAsync(checks, "PRODUCT_CATEGORY", "Product categories", await db.ProductCategories.CountAsync(cancellationToken), 1, "Product categories exist.", "Create product categories/groups before product entry.");
        await AddCountCheckAsync(checks, "PRODUCT", "Product master", await db.Products.CountAsync(cancellationToken), 1, "Product master has at least one item.", "Create products manually or seed demo data for training.");
        await AddCountCheckAsync(checks, "STOCK", "Stock rows", await db.Stocks.CountAsync(cancellationToken), 1, "Stock rows exist.", "Add opening stock or purchase inward.");
        await AddCountCheckAsync(checks, "CUSTOMER", "Customer master", await db.Customers.CountAsync(cancellationToken), 1, "Customer master has at least one customer.", "Create walk-in/demo customer before POS testing.");
        await AddCountCheckAsync(checks, "VENDOR", "Vendor master", await db.Vendors.CountAsync(cancellationToken), 1, "Vendor master has at least one vendor.", "Create vendor before purchase inward testing.");
        await AddCountCheckAsync(checks, "SALESMAN", "Salesman master", await db.Salesmen.CountAsync(cancellationToken), 1, "Salesman master has at least one active salesman.", "Run Quick Start or create a salesman.");

        var negativeStockCount = await db.Stocks.CountAsync(stock => stock.PurchaseQty - stock.SoldQty < 0, cancellationToken);
        Add(checks,
            "NEGATIVE_STOCK",
            "Negative stock",
            negativeStockCount == 0 ? "Pass" : "Critical",
            negativeStockCount == 0 ? "Info" : "High",
            negativeStockCount == 0 ? "No negative stock rows found." : $"{negativeStockCount} stock row(s) are negative.",
            "Open Admin → Consistency & Repair and review stock ledger/quantity mismatch before go-live.");

        var missingHsnCount = await db.Products.CountAsync(product => product.HSNCode == null || product.HSNCode == string.Empty, cancellationToken);
        Add(checks,
            "PRODUCT_HSN",
            "Product HSN coverage",
            missingHsnCount == 0 ? "Pass" : "Warning",
            missingHsnCount == 0 ? "Info" : "Medium",
            missingHsnCount == 0 ? "All products have HSN codes." : $"{missingHsnCount} product(s) are missing HSN codes.",
            "Update Product Master so GST HSN reports are accurate.");

        var backupDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "backups");
        var backupDirectoryExists = Directory.Exists(backupDirectory) || Directory.Exists("backups") || Directory.Exists("/backups");
        Add(checks,
            "BACKUP_PATH",
            "Backup path mounted",
            backupDirectoryExists ? "Pass" : "Warning",
            backupDirectoryExists ? "Info" : "Medium",
            backupDirectoryExists ? "A backup directory is visible to the API process." : "No standard backup directory was visible to the API process.",
            "Verify Backup settings and run System Health → Backup before live billing.");

        var critical = checks.Count(IsCritical);
        var warnings = checks.Count(item => item.Status.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        var passed = checks.Count(item => item.Status.Equals("Pass", StringComparison.OrdinalIgnoreCase));
        var status = critical > 0 ? "Blocked" : warnings > 0 ? "Needs attention" : "Ready";

        return Results.Ok(new ReleaseSmokeSummaryDto(status, DateTimeOffset.UtcNow, passed, warnings, critical, checks));
    }

    private static async Task<IResult> SeedDemoDataAsync(
        DemoSeedRequest request,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var counters = new DemoSeedCounters();
        var notes = new List<string>
        {
            "Demo seed is idempotent: it creates missing demo masters and leaves existing rows unchanged.",
            "Training invoices are intentionally not generated here; use the UI to test billing/purchase flows with audit visibility."
        };

        var company = await db.Companies.FirstOrDefaultAsync(item => item.Code == "DEMO" || item.Name == "Garmetix Demo Company", cancellationToken);
        if (company is null)
        {
            company = new Company
            {
                Name = "Garmetix Demo Company",
                Code = "DEMO",
                Active = true,
                ContactNumber = "9999999999",
                Email = "demo@garmetix.local",
                Address = "Main Road",
                City = "Dumka",
                State = "Jharkhand",
                Country = "India",
                ZipCode = "814101",
                GSTIN = "20AAAAA0000A1Z5",
                Pan = "AAAAA0000A",
                ContactPerson = "Demo Admin",
                ContactMobile = "9999999999"
            };
            db.Companies.Add(company);
            counters.Companies++;
        }

        var storeGroup = await db.StoreGroups.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.GroupCode == "DEMO", cancellationToken);
        if (storeGroup is null)
        {
            storeGroup = new StoreGroup
            {
                Name = "Demo Main Group",
                GroupCode = "DEMO",
                Active = true,
                CompanyId = company.Id
            };
            db.StoreGroups.Add(storeGroup);
            counters.StoreGroups++;
        }

        var store = await db.Stores.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.StoreGroupId == storeGroup.Id && item.StoreCode == "DEMO", cancellationToken);
        if (store is null)
        {
            store = new Store
            {
                Name = "Demo Retail Store",
                StoreCode = "DEMO",
                Active = true,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                ContactNumber = "9999999999",
                Email = "store@garmetix.local",
                Address = "Main Road",
                City = "Dumka",
                State = "Jharkhand",
                Country = "India",
                ZipCode = "814101"
            };
            db.Stores.Add(store);
            counters.Stores++;
        }

        var category = await db.ProductCategories.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Name == "Demo Readymade", cancellationToken);
        if (category is null)
        {
            category = new InventoryProductCategory
            {
                Name = "Demo Readymade",
                ProductGroup = ProductGroup.Readymade,
                IsActive = true,
                CompanyId = company.Id
            };
            db.ProductCategories.Add(category);
            counters.ProductCategories++;
        }

        var subCategory = await db.ProductSubCategories.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.CategoryId == category.Id && item.Name == "Demo Shirts", cancellationToken);
        if (subCategory is null)
        {
            subCategory = new InventoryProductSubCategory
            {
                Name = "Demo Shirts",
                CategoryId = category.Id,
                CompanyId = company.Id
            };
            db.ProductSubCategories.Add(subCategory);
            counters.ProductSubCategories++;
        }

        var tax = await EnsureTaxAsync(db, "GST 5", 5, TaxType.GST, counters, cancellationToken);
        await EnsureTaxAsync(db, "GST 12", 12, TaxType.GST, counters, cancellationToken);
        await EnsureTaxAsync(db, "GST 18", 18, TaxType.GST, counters, cancellationToken);

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.MobileNumber == "8888888888", cancellationToken);
        if (vendor is null)
        {
            vendor = new Vendor
            {
                Name = "Demo Supplier",
                Address = "Supplier Market",
                City = "Dumka",
                ZipCode = "814101",
                MobileNumber = "8888888888",
                Email = "supplier@garmetix.local",
                GSTIN = "20BBBBB1111B1Z5",
                Active = true,
                CompanyId = company.Id
            };
            db.Vendors.Add(vendor);
            counters.Vendors++;
        }

        var customer = await db.Customers.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.MobileNumber == "7777777777", cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                Name = "Demo Walk-in Customer",
                MobileNumber = "7777777777",
                Email = "customer@garmetix.local",
                GSTIN = "20CCCCC2222C1Z5",
                Address = "Customer Street",
                City = "Dumka",
                State = "Jharkhand",
                ZipCode = "814101",
                Country = "India",
                Registred = true,
                CompanyId = company.Id
            };
            db.Customers.Add(customer);
            counters.Customers++;
        }

        var salesman = await db.Salesmen.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.StoreId == store.Id && item.Name == "Demo Salesman", cancellationToken);
        if (salesman is null)
        {
            salesman = new Salesman
            {
                Name = "Demo Salesman",
                Active = true,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                StoreId = store.Id
            };
            db.Salesmen.Add(salesman);
            counters.Salesmen++;
        }

        var brand = await db.Brands.FirstOrDefaultAsync(item => item.BrandCode == "DEMO", cancellationToken);
        if (brand is null)
        {
            brand = new Brand
            {
                Name = "Demo Brand",
                BrandCode = "DEMO",
                SupplierId = vendor.Id
            };
            db.Brands.Add(brand);
            counters.Brands++;
        }

        await EnsureDemoProductAsync(db, counters, company, storeGroup, store, category, subCategory, tax, vendor, "DEMO-SHIRT-001", "Demo Cotton Shirt", "6105", ProductType.Apparels, ProductGroup.Readymade, 999m, 25m, 520m, "DS-001", "White", "Demo Brand", cancellationToken);
        await EnsureDemoProductAsync(db, counters, company, storeGroup, store, category, subCategory, tax, vendor, "DEMO-SUIT-001", "Demo Suiting Item", "5515", ProductType.Fabric, ProductGroup.Suiting, 1499m, 12m, 850m, "DSU-001", "Navy", "Demo Brand", cancellationToken);
        await EnsureDemoProductAsync(db, counters, company, storeGroup, store, category, subCategory, tax, vendor, "DEMO-SHOE-001", "Demo Ethnic Shoe", "6403", ProductType.Shoes, ProductGroup.Shoes, 1299m, 10m, 700m, "DSH-001", "Brown", "Demo Brand", cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        if (request.IncludeTrainingTransactions)
        {
            notes.Add("IncludeTrainingTransactions was requested but is not active in v1.6; create sample sales/purchases from the UI so stock, tax, and accounting workflows are exercised normally.");
        }

        return Results.Ok(new DemoSeedResponse(
            "Demo/training master data is ready.",
            DateTimeOffset.UtcNow,
            counters.ToDto(),
            new DemoSeedIdsDto(company.Id, storeGroup.Id, store.Id, category.Id, subCategory.Id, tax.Id, vendor.Id, customer.Id, salesman.Id),
            notes));
    }

    private static async Task<Tax> EnsureTaxAsync(GarmetixDbContext db, string name, decimal rate, TaxType taxType, DemoSeedCounters counters, CancellationToken cancellationToken)
    {
        var tax = await db.Taxes.FirstOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (tax is not null)
        {
            return tax;
        }

        tax = new Tax
        {
            Name = name,
            CompositeRate = rate,
            TaxType = taxType
        };
        db.Taxes.Add(tax);
        counters.Taxes++;
        return tax;
    }

    private static async Task EnsureDemoProductAsync(
        GarmetixDbContext db,
        DemoSeedCounters counters,
        Company company,
        StoreGroup storeGroup,
        Store store,
        InventoryProductCategory category,
        InventoryProductSubCategory subCategory,
        Tax tax,
        Vendor vendor,
        string barcode,
        string name,
        string hsn,
        ProductType productType,
        ProductGroup productGroup,
        decimal mrp,
        decimal openingQty,
        decimal costPrice,
        string styleCode,
        string baseColor,
        string brandName,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Barcode == barcode, cancellationToken);
        if (product is null)
        {
            product = new Product
            {
                Name = name,
                Barcode = barcode,
                Descriptions = "Seeded demo product for training and smoke testing.",
                HSNCode = hsn,
                MRP = mrp,
                TaxRate = tax.CompositeRate,
                TaxType = tax.TaxType,
                Unit = Unit.Pcs,
                ProductType = productType,
                ProductGroup = productGroup,
                ProductCategoryId = category.Id,
                ProductSubCategoryId = subCategory.Id,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id
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
                Unit = Unit.Pcs,
                PurchaseQty = openingQty,
                CostPrice = costPrice,
                SoldQty = 0,
                MRP = mrp,
                TaxRate = tax.CompositeRate,
                TaxType = tax.TaxType,
                TaxId = tax.Id,
                StockType = StockType.Opening,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                StoreId = store.Id
            };
            db.Stocks.Add(stock);
            counters.Stocks++;

            db.StockMovements.Add(new StockMovement
            {
                StockId = stock.Id,
                ProductId = product.Id,
                Barcode = barcode,
                MovementType = "DemoOpening",
                QuantityIn = openingQty,
                QuantityOut = 0,
                CostPrice = costPrice,
                MRP = mrp,
                TaxRate = tax.CompositeRate,
                HSNCode = hsn,
                SourceType = "DemoSeed",
                SourceId = product.Id,
                SourceNumber = barcode,
                Remarks = "Demo opening stock seeded for smoke testing.",
                OnDate = DateTime.Now,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                StoreId = store.Id
            });
            counters.StockMovements++;
        }

        var detail = await db.ProductDetails.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.ProductId == product.Id && item.Barcode == barcode, cancellationToken);
        if (detail is null)
        {
            db.ProductDetails.Add(new ProductDetail
            {
                ProductId = product.Id,
                Barcode = barcode,
                StyleCode = styleCode,
                BaseColor = baseColor,
                Brand = brandName,
                VendorId = vendor.Id,
                CompanyId = company.Id
            });
            counters.ProductDetails++;
        }
    }

    private static Task AddCountCheckAsync(List<ReleaseSmokeCheckDto> checks, string code, string title, int count, int minimum, string passMessage, string fixHint)
    {
        Add(checks,
            code,
            title,
            count >= minimum ? "Pass" : "Warning",
            count >= minimum ? "Info" : "Medium",
            count >= minimum ? $"{passMessage} Count: {count}." : $"Expected at least {minimum}, found {count}.",
            fixHint);
        return Task.CompletedTask;
    }

    private static void Add(List<ReleaseSmokeCheckDto> checks, string code, string title, string status, string severity, string message, string fixHint)
    {
        checks.Add(new ReleaseSmokeCheckDto(code, title, status, severity, message, fixHint));
    }

    private static bool IsCritical(ReleaseSmokeCheckDto check)
    {
        return check.Status.Equals("Critical", StringComparison.OrdinalIgnoreCase);
    }
}
