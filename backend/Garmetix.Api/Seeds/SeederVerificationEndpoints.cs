using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Seeds;

public sealed record SeederVerificationCheckDto(
    string Key,
    string Label,
    bool Passed,
    string Status,
    string Message,
    int ExpectedCount,
    int ActualCount);

public sealed record SeederVerificationStatusDto(
    DateTimeOffset CheckedAtUtc,
    bool Ready,
    IReadOnlyList<SeederVerificationCheckDto> Checks,
    IReadOnlyList<string> Recommendations);

public static class SeederVerificationEndpoints
{
    public static RouteGroupBuilder MapSeederVerificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/seeder-verification")
            .WithTags("Seeder Verification")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", StatusAsync);

        return group;
    }

    private static async Task<SeederVerificationStatusDto> StatusAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var checks = new List<SeederVerificationCheckDto>();

        var aadwika = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(company => company.Code == "AF" || company.Name == "Aadwika Fashion", cancellationToken);
        checks.Add(Check(
            "aadwika-company",
            "Aadwika Fashion company",
            aadwika is not null,
            1,
            aadwika is null ? 0 : 1,
            aadwika is null ? "Aadwika Fashion company is missing." : "Aadwika Fashion company exists."));

        var mbo = aadwika is null
            ? null
            : await db.StoreGroups.AsNoTracking()
                .FirstOrDefaultAsync(group => group.CompanyId == aadwika.Id && (group.GroupCode == "MBO" || group.Name == "Aadwika Fashion MBO"), cancellationToken);
        checks.Add(Check(
            "aadwika-mbo-group",
            "Aadwika Fashion MBO store group",
            mbo is not null,
            1,
            mbo is null ? 0 : 1,
            mbo is null ? "Aadwika Fashion MBO store group is missing." : "Aadwika Fashion MBO store group exists."));

        var aadwikaStore = aadwika is null || mbo is null
            ? null
            : await db.Stores.AsNoTracking()
                .FirstOrDefaultAsync(store => store.CompanyId == aadwika.Id && store.StoreGroupId == mbo.Id && (store.StoreCode == "AFMBO" || store.Name.Contains("Aadwika Fashion MBO")), cancellationToken);
        checks.Add(Check(
            "aadwika-mbo-store",
            "Aadwika Fashion MBO store",
            aadwikaStore is not null,
            1,
            aadwikaStore is null ? 0 : 1,
            aadwikaStore is null ? "Aadwika Fashion MBO store is missing." : "Aadwika Fashion MBO store exists."));

        var smartStore = aadwika is null || mbo is null
            ? null
            : await db.Stores.AsNoTracking()
                .FirstOrDefaultAsync(store => store.CompanyId == aadwika.Id && store.StoreGroupId == mbo.Id && (store.StoreCode == "SM01" || store.Name.Contains("Smart Menswear")), cancellationToken);
        checks.Add(Check(
            "smart-menswear-store",
            "Smart Menswear store under Aadwika Fashion MBO",
            smartStore is not null,
            1,
            smartStore is null ? 0 : 1,
            smartStore is null ? "Smart Menswear store is not under Aadwika Fashion MBO." : "Smart Menswear store is under Aadwika Fashion MBO."));

        var shalini = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(company => company.Code == "AFS" || company.Name.Contains("Shalini"), cancellationToken);
        checks.Add(Check(
            "shalini-separate",
            "Aadwika Fashion - Shalini remains separate",
            shalini is not null && (aadwika is null || shalini.Id != aadwika.Id),
            1,
            shalini is null ? 0 : 1,
            shalini is null ? "Shalini company/profile is missing." : "Shalini company/profile is separate."));

        if (aadwika is not null)
        {
            var groupCount = await db.LedgerGroups.AsNoTracking()
                .CountAsync(group => group.CompanyId == aadwika.Id && AccountingDefaultProtection.ProtectedLedgerGroupNames.Contains(group.Name), cancellationToken);
            var ledgerCount = await db.Ledgers.AsNoTracking()
                .CountAsync(ledger => ledger.CompanyId == aadwika.Id && AccountingDefaultProtection.ProtectedLedgerNames.Contains(ledger.Name), cancellationToken);

            checks.Add(Check(
                "default-ledger-groups",
                "Protected default ledger groups",
                groupCount >= AccountingDefaultProtection.ProtectedLedgerGroupNames.Count / 2,
                AccountingDefaultProtection.ProtectedLedgerGroupNames.Count,
                groupCount,
                $"{groupCount} protected default ledger group name(s) found for Aadwika Fashion."));

            checks.Add(Check(
                "default-ledgers",
                "Protected default ledgers",
                ledgerCount >= AccountingDefaultProtection.ProtectedLedgerNames.Count / 2,
                AccountingDefaultProtection.ProtectedLedgerNames.Count,
                ledgerCount,
                $"{ledgerCount} protected default ledger name(s) found for Aadwika Fashion."));
        }

        var duplicateSmartCompanies = await db.Companies.AsNoTracking()
            .CountAsync(company => company.Name.Contains("Smart Menswear") || company.Name.Contains("Samrat Menswear") || company.Code == "SM", cancellationToken);
        checks.Add(Check(
            "old-smart-company",
            "Old Smart/Samrat company cleaned",
            duplicateSmartCompanies == 0,
            0,
            duplicateSmartCompanies,
            duplicateSmartCompanies == 0 ? "No old Smart/Samrat company remains." : $"{duplicateSmartCompanies} old Smart/Samrat company row(s) still exist. Use merge preview/apply."));

        var ready = checks.All(check => check.Passed);
        var recommendations = new List<string>
        {
            "After AF/SS seed, portable import, or Smart merge, run this verification.",
            "If old Smart/Samrat company remains, run Aadwika + Smart Menswear Merge preview/apply.",
            "Run Data Consistency after verification before go-live."
        };

        return new SeederVerificationStatusDto(DateTimeOffset.UtcNow, ready, checks, recommendations);
    }

    private static SeederVerificationCheckDto Check(string key, string label, bool passed, int expected, int actual, string message)
        => new(key, label, passed, passed ? "Passed" : "Action required", message, expected, actual);
}
