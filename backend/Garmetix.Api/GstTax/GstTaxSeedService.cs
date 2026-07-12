using Garmetix.Core.Models.GstTax;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>
/// One-time (idempotent, re-runnable) seed of GST &amp; Taxes reference data: Indian GST state codes,
/// common UQC codes, the configurable audit-rule catalog, a default garment-threshold tax rate rule,
/// and the "Local Master" provider so the module works out of the box with zero external API setup.
/// </summary>
public static class GstTaxSeedService
{
    public static async Task EnsureSeedDataAsync(GarmetixDbContext db, CancellationToken cancellationToken = default)
    {
        await SeedStateCodesAsync(db, cancellationToken);
        await SeedUqcCodesAsync(db, cancellationToken);
        await SeedAuditRulesAsync(db, cancellationToken);
        await SeedGarmentTaxRateRulesAsync(db, cancellationToken);
        await SeedLocalMasterProviderAsync(db, cancellationToken);
    }

    private static async Task SeedStateCodesAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (await db.GstStateCodes.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        (string Code, string Name, bool IsUt, bool IsOther)[] states =
        [
            ("01", "Jammu and Kashmir", false, false),
            ("02", "Himachal Pradesh", false, false),
            ("03", "Punjab", false, false),
            ("04", "Chandigarh", true, false),
            ("05", "Uttarakhand", false, false),
            ("06", "Haryana", false, false),
            ("07", "Delhi", true, false),
            ("08", "Rajasthan", false, false),
            ("09", "Uttar Pradesh", false, false),
            ("10", "Bihar", false, false),
            ("11", "Sikkim", false, false),
            ("12", "Arunachal Pradesh", false, false),
            ("13", "Nagaland", false, false),
            ("14", "Manipur", false, false),
            ("15", "Mizoram", false, false),
            ("16", "Tripura", false, false),
            ("17", "Meghalaya", false, false),
            ("18", "Assam", false, false),
            ("19", "West Bengal", false, false),
            ("20", "Jharkhand", false, false),
            ("21", "Odisha", false, false),
            ("22", "Chattisgarh", false, false),
            ("23", "Madhya Pradesh", false, false),
            ("24", "Gujarat", false, false),
            ("26", "Dadra and Nagar Haveli and Daman and Diu", true, false),
            ("27", "Maharashtra", false, false),
            ("29", "Karnataka", false, false),
            ("30", "Goa", false, false),
            ("31", "Lakshadweep", true, false),
            ("32", "Kerala", false, false),
            ("33", "Tamil Nadu", false, false),
            ("34", "Puducherry", true, false),
            ("35", "Andaman and Nicobar Islands", true, false),
            ("36", "Telangana", false, false),
            ("37", "Andhra Pradesh", false, false),
            ("38", "Ladakh", true, false),
            ("97", "Other Territory", false, true),
            ("99", "Centre Jurisdiction", false, true)
        ];

        foreach (var state in states)
        {
            db.GstStateCodes.Add(new GstStateCode
            {
                StateCode = state.Code,
                StateName = state.Name,
                IsUnionTerritory = state.IsUt,
                IsOtherTerritory = state.IsOther
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedUqcCodesAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (await db.GstUqcCodes.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        (string Code, string Description)[] codes =
        [
            ("PCS", "Pieces"),
            ("NOS", "Numbers"),
            ("MTR", "Meters"),
            ("KG", "Kilograms"),
            ("GM", "Grams"),
            ("LTR", "Litres"),
            ("BOX", "Box"),
            ("SET", "Set"),
            ("PAIR", "Pairs")
        ];

        foreach (var code in codes)
        {
            db.GstUqcCodes.Add(new GstUqcCode { UqcCode = code.Code, Description = code.Description });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAuditRulesAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (await db.GstAuditRules.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        (string Code, string Name, string Module, string Severity)[] rules =
        [
            ("GSTIN_FORMAT_INVALID", "GSTIN format is invalid", "Customer", "Error"),
            ("GSTIN_INACTIVE", "GSTIN is not active", "Customer", "Warning"),
            ("GSTIN_STATE_MISMATCH", "GSTIN state code does not match party state", "Customer", "Warning"),
            ("HSN_MISSING", "Product is missing an HSN/SAC code", "Product", "Warning"),
            ("HSN_INVALID", "HSN/SAC code is not a recognized format or master entry", "Product", "Error"),
            ("GST_RATE_MISSING", "Product has no resolvable GST rate", "Product", "Warning"),
            ("GST_RATE_MISMATCH", "Product's configured GST rate differs from the resolved rate", "Product", "Warning"),
            ("CGST_SGST_NOT_EQUAL", "CGST and SGST amounts are not equal on an intra-state line", "Sale", "Error"),
            ("INTRASTATE_IGST_USED", "IGST was charged on an intra-state invoice", "Sale", "Error"),
            ("INTERSTATE_CGST_SGST_USED", "CGST/SGST was charged on an inter-state invoice", "Sale", "Error"),
            ("GARMENT_THRESHOLD_RATE_MISMATCH", "Garment GST rate does not match the configured price threshold slab", "Sale", "Warning"),
            ("SALE_LINE_TAX_MISMATCH", "Sale invoice line tax amount does not match the expected calculation", "Sale", "Warning"),
            ("PURCHASE_LINE_TAX_MISMATCH", "Purchase invoice line tax amount does not match the expected calculation", "Purchase", "Warning"),
            ("INVOICE_TOTAL_TAX_MISMATCH", "Invoice total tax does not match the sum of its lines", "Sale", "Warning"),
            ("B2B_GSTIN_REQUIRED", "B2B invoice is missing a customer GSTIN", "Sale", "Warning"),
            ("UQC_MISSING", "Product/HSN is missing a Unit Quantity Code", "Product", "Info"),
            ("PINCODE_STATE_MISMATCH", "Pincode does not match the declared state", "Customer", "Info")
        ];

        foreach (var rule in rules)
        {
            db.GstAuditRules.Add(new GstAuditRule
            {
                RuleCode = rule.Code,
                RuleName = rule.Name,
                ModuleArea = rule.Module,
                Severity = rule.Severity,
                IsEnabled = true,
                StrictMode = false
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedGarmentTaxRateRulesAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (await db.GstTaxRateRules.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        var effectiveFrom = new DateTime(2017, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);
        const string seedNotice = "Seeded default - verify against the current CBIC garment GST slabs for your business before relying on this for filing. Fully editable in GST & Taxes -> Rate Master.";

        db.GstTaxRateRules.Add(new GstTaxRateRule
        {
            RuleName = "Garment GST Threshold - Up To Rs.1000 (Default Seed)",
            ProductCategory = "Garments",
            GoodsOrService = "Goods",
            TaxRate = 5m,
            ThresholdBasis = "BasicRateAfterDiscount",
            PriceThresholdTo = 1000m,
            EffectiveFrom = effectiveFrom,
            Priority = 100,
            IsActive = true,
            Notes = seedNotice
        });

        db.GstTaxRateRules.Add(new GstTaxRateRule
        {
            RuleName = "Garment GST Threshold - Above Rs.1000 (Default Seed)",
            ProductCategory = "Garments",
            GoodsOrService = "Goods",
            TaxRate = 18m,
            ThresholdBasis = "BasicRateAfterDiscount",
            PriceThresholdFrom = 1000.01m,
            EffectiveFrom = effectiveFrom,
            Priority = 100,
            IsActive = true,
            Notes = seedNotice
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedLocalMasterProviderAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (await db.GstApiProviders.AsNoTracking().AnyAsync(p => p.ProviderType == "LocalMasterOnly", cancellationToken))
        {
            return;
        }

        var provider = new GstApiProvider
        {
            ProviderName = "Local Master",
            ProviderType = "LocalMasterOnly",
            Environment = "Production",
            IsEnabled = true,
            Priority = 999,
            FallbackEnabled = true,
            Notes = "Built-in provider backed by the local HSN/rate/GSTIN-cache masters. Always available, requires no API setup, and is the last-resort fallback for every GST feature.",
            CreatedBy = "System Seed"
        };
        db.GstApiProviders.Add(provider);
        await db.SaveChangesAsync(cancellationToken);

        string[] features = ["GSTIN_LOOKUP", "HSN_LOOKUP", "GST_RATE_LOOKUP"];
        foreach (var feature in features)
        {
            db.GstApiProviderFeatures.Add(new GstApiProviderFeature
            {
                ProviderId = provider.Id,
                FeatureCode = feature,
                IsEnabled = true,
                Priority = 999
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
