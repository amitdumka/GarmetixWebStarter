using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Audit;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsExchangeService(GarmetixDbContext db, FinalAccountsReportService reports)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public async Task<IReadOnlyList<FinalAccountsTallyProfileDto>> ListProfilesAsync(
        FinalAccountsTallyProfileQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var rows = ProfilesInScope(scope).AsNoTracking();
        if (query.ActiveOnly == true)
        {
            rows = rows.Where(item => item.IsActive);
        }

        return await rows.OrderBy(item => item.ProfileCode).Select(item => ToProfileDto(item)).ToListAsync(cancellationToken);
    }

    public async Task<FinalAccountsTallyProfileDto> CreateProfileAsync(
        FinalAccountsTallyProfileRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var actor = ResolveActor(context);
        var profile = new FinalAccountsTallyProfile
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            ProfileCode = FinalAccountsExchangeRules.NormalizeText(request.ProfileCode, "Profile code", 64).ToUpperInvariant(),
            Name = FinalAccountsExchangeRules.NormalizeText(request.Name, "Profile name", 160),
            TallyRelease = FinalAccountsExchangeRules.NormalizeText(request.TallyRelease, "Tally release", 80),
            TestCompanyName = FinalAccountsExchangeRules.NormalizeText(request.TestCompanyName, "Test company name", 160),
            BaseCurrency = FinalAccountsExchangeRules.NormalizeText(request.BaseCurrency, "Base currency", 16),
            Country = FinalAccountsExchangeRules.NormalizeText(request.Country, "Country", 80),
            GstRegistrationType = FinalAccountsExchangeRules.OptionalText(request.GstRegistrationType, 80),
            DuplicatePolicy = FinalAccountsExchangeRules.ParseDuplicatePolicy(request.DuplicatePolicy),
            DirectPostingAllowed = false,
            GroupMappingJson = FinalAccountsExchangeRules.ToJson(request.GroupMapping),
            LedgerMappingJson = FinalAccountsExchangeRules.ToJson(request.LedgerMapping),
            VoucherTypeMappingJson = FinalAccountsExchangeRules.ToJson(request.VoucherTypeMapping),
            TaxMappingJson = FinalAccountsExchangeRules.ToJson(request.TaxMapping),
            StockCostCentreMappingJson = FinalAccountsExchangeRules.ToJson(request.StockCostCentreMapping),
            IsActive = request.IsActive,
            Notes = FinalAccountsExchangeRules.OptionalText(request.Notes, 1000),
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        EnsureCanWrite(profile, context);
        db.FinalAccountsTallyProfiles.Add(profile);
        AddAudit(context, "CreateProfile", nameof(FinalAccountsTallyProfile), profile.Id, profile.ProfileCode, scope, actor, null, new { profile.Name, profile.TallyRelease });
        await db.SaveChangesAsync(cancellationToken);
        return ToProfileDto(profile);
    }

    public async Task<FinalAccountsTallyProfileDto> UpdateProfileAsync(
        Guid id,
        FinalAccountsTallyProfileRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var profile = await ProfilesInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Tally profile was not found for the selected scope.");
        var actor = ResolveActor(context);
        var before = new { profile.Name, profile.TallyRelease, profile.DuplicatePolicy, profile.IsActive };
        profile.ProfileCode = FinalAccountsExchangeRules.NormalizeText(request.ProfileCode, "Profile code", 64).ToUpperInvariant();
        profile.Name = FinalAccountsExchangeRules.NormalizeText(request.Name, "Profile name", 160);
        profile.TallyRelease = FinalAccountsExchangeRules.NormalizeText(request.TallyRelease, "Tally release", 80);
        profile.TestCompanyName = FinalAccountsExchangeRules.NormalizeText(request.TestCompanyName, "Test company name", 160);
        profile.BaseCurrency = FinalAccountsExchangeRules.NormalizeText(request.BaseCurrency, "Base currency", 16);
        profile.Country = FinalAccountsExchangeRules.NormalizeText(request.Country, "Country", 80);
        profile.GstRegistrationType = FinalAccountsExchangeRules.OptionalText(request.GstRegistrationType, 80);
        profile.DuplicatePolicy = FinalAccountsExchangeRules.ParseDuplicatePolicy(request.DuplicatePolicy);
        profile.DirectPostingAllowed = false;
        profile.GroupMappingJson = FinalAccountsExchangeRules.ToJson(request.GroupMapping);
        profile.LedgerMappingJson = FinalAccountsExchangeRules.ToJson(request.LedgerMapping);
        profile.VoucherTypeMappingJson = FinalAccountsExchangeRules.ToJson(request.VoucherTypeMapping);
        profile.TaxMappingJson = FinalAccountsExchangeRules.ToJson(request.TaxMapping);
        profile.StockCostCentreMappingJson = FinalAccountsExchangeRules.ToJson(request.StockCostCentreMapping);
        profile.IsActive = request.IsActive;
        profile.Notes = FinalAccountsExchangeRules.OptionalText(request.Notes, 1000);
        profile.Revision += 1;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = actor;
        EnsureCanWrite(profile, context);
        AddAudit(context, "UpdateProfile", nameof(FinalAccountsTallyProfile), profile.Id, profile.ProfileCode, scope, actor, before, new { profile.Name, profile.TallyRelease, profile.DuplicatePolicy, profile.IsActive });
        await db.SaveChangesAsync(cancellationToken);
        return ToProfileDto(profile);
    }

    public async Task<FinalAccountsExchangePreviewResponse> PreviewTallyAsync(
        FinalAccountsExchangeRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var profile = await ResolveProfileAsync(scope, request.TallyProfileId, cancellationToken);
        return await BuildTallyPreviewAsync(scope, profile, request.From.Date, request.To.Date, request.AsOf?.Date ?? request.To.Date, request.Format, cancellationToken);
    }

    public async Task<FinalAccountsReportExport> ExportTallyAsync(
        FinalAccountsExchangeRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var profile = await ResolveProfileAsync(scope, request.TallyProfileId, cancellationToken);
        var preview = await BuildTallyPreviewAsync(scope, profile, request.From.Date, request.To.Date, request.AsOf?.Date ?? request.To.Date, request.Format, cancellationToken);
        var actor = ResolveActor(context);
        var run = await CreateRunAsync(scope, FinalAccountsExchangeRunKind.TallyExchange, profile.Id, request.From.Date, request.To.Date, request.AsOf?.Date ?? request.To.Date, preview, request.Format, request.Notes, actor, context, cancellationToken);

        var format = FinalAccountsExchangeRules.NormalizeFormat(request.Format);
        var fileName = $"{run.RunNumber}-tally.{format}";
        byte[] bytes;
        string contentType;
        if (format == "xml")
        {
            bytes = Encoding.UTF8.GetBytes(preview.XmlFixture);
            contentType = "application/xml";
        }
        else if (format == "json")
        {
            bytes = Encoding.UTF8.GetBytes(preview.JsonFixture);
            contentType = "application/json";
        }
        else if (format == "csv")
        {
            bytes = Encoding.UTF8.GetBytes(BuildPreviewCsv(preview));
            contentType = "text/csv";
        }
        else
        {
            bytes = BuildTallyZip(run.RunNumber, profile, preview);
            contentType = "application/zip";
        }

        run.Status = FinalAccountsExchangeRunStatus.Generated;
        run.GeneratedAt = DateTime.UtcNow;
        run.GeneratedBy = actor;
        run.FileName = fileName;
        run.ZipChecksum = FinalAccountsExchangeRules.Sha256Hex(bytes);
        run.UpdatedAt = DateTime.UtcNow;
        run.UpdatedBy = actor;
        AddAudit(context, "GenerateTallyExport", nameof(FinalAccountsExchangeRun), run.Id, run.RunNumber, scope, actor, null, new { run.FileName, run.ZipChecksum });
        await db.SaveChangesAsync(cancellationToken);
        return new FinalAccountsReportExport(fileName, contentType, bytes);
    }

    public async Task<FinalAccountsExchangePreviewResponse> PreviewCaPackageAsync(
        FinalAccountsCaPackageRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var items = await BuildCaPackageItemsAsync(scope, request, context, includeBytes: false, cancellationToken);
        var exceptions = BuildPackageExceptions(items);
        return new FinalAccountsExchangePreviewResponse(
            "CaPackage",
            request.From.Date,
            request.To.Date,
            request.AsOf.Date,
            "zip",
            false,
            FinalAccountsTallyDuplicatePolicy.RejectDuplicate.ToString(),
            0,
            0,
            0m,
            0m,
            0m,
            Array.Empty<FinalAccountsExchangeMappingRowDto>(),
            exceptions,
            string.Empty,
            FinalAccountsExchangeRules.BuildJsonFixture(items.Select(item => item.Dto)),
            items.Select(item => item.Dto).ToList());
    }

    public async Task<FinalAccountsReportExport> ExportCaPackageAsync(
        FinalAccountsCaPackageRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var actor = ResolveActor(context);
        var preview = await PreviewCaPackageAsync(request, context, cancellationToken);
        var run = await CreateRunAsync(scope, FinalAccountsExchangeRunKind.CaPackage, null, request.From.Date, request.To.Date, request.AsOf.Date, preview, "zip", request.Notes, actor, context, cancellationToken);
        var items = await BuildCaPackageItemsAsync(scope, request, context, includeBytes: true, cancellationToken);
        var bytes = BuildCaPackageZip(run.RunNumber, request, items);
        run.Status = FinalAccountsExchangeRunStatus.Generated;
        run.GeneratedAt = DateTime.UtcNow;
        run.GeneratedBy = actor;
        run.FileName = $"{run.RunNumber}-ca-package.zip";
        run.ZipChecksum = FinalAccountsExchangeRules.Sha256Hex(bytes);
        run.PayloadHash = FinalAccountsExchangeRules.Sha256Hex(string.Join("|", items.Select(item => item.Dto.Sha256)));
        run.ExceptionCount = BuildPackageExceptions(items).Count;
        run.UpdatedAt = DateTime.UtcNow;
        run.UpdatedBy = actor;
        AddAudit(context, "GenerateCaPackage", nameof(FinalAccountsExchangeRun), run.Id, run.RunNumber, scope, actor, null, new { run.FileName, run.ZipChecksum, ItemCount = items.Count });
        await db.SaveChangesAsync(cancellationToken);
        return new FinalAccountsReportExport(run.FileName, "application/zip", bytes);
    }

    public async Task<FinalAccountsExchangeRunListResponse> ListRunsAsync(
        FinalAccountsExchangeRunQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var rowsQuery = db.FinalAccountsExchangeRuns.AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);
        if (!string.IsNullOrWhiteSpace(query.RunKind) && !string.Equals(query.RunKind, "All", StringComparison.OrdinalIgnoreCase))
        {
            var kind = FinalAccountsExchangeRules.ParseRunKind(query.RunKind);
            rowsQuery = rowsQuery.Where(item => item.RunKind == kind);
        }

        var totalCount = await rowsQuery.CountAsync(cancellationToken);
        var rows = await rowsQuery
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new FinalAccountsExchangeRunDto(
                item.Id,
                item.RunNumber,
                item.RunKind.ToString(),
                item.Status.ToString(),
                item.TallyProfileId,
                item.PeriodFrom,
                item.PeriodTo,
                item.AsOf,
                item.Format,
                item.MasterCount,
                item.VoucherCount,
                item.ExceptionCount,
                item.ControlDebit,
                item.ControlCredit,
                item.PayloadHash,
                item.ZipChecksum,
                item.FileName,
                item.CreatedAt,
                item.GeneratedAt,
                item.GeneratedBy))
            .ToListAsync(cancellationToken);
        return new FinalAccountsExchangeRunListResponse(page, pageSize, totalCount, rows);
    }

    private async Task<FinalAccountsExchangePreviewResponse> BuildTallyPreviewAsync(
        FinalAccountsScopeDto scope,
        FinalAccountsTallyProfile profile,
        DateTime from,
        DateTime to,
        DateTime asOf,
        string? format,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ArgumentException("Exchange end date must be on or after the start date.");
        }

        var accounts = await db.FinalAccountsAccounts.AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        var groups = await db.FinalAccountsAccountGroups.AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        var journalEntries = await db.FinalAccountsJournalEntries.AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId && item.OnDate >= from && item.OnDate <= to && item.Status == FinalAccountsJournalStatus.Posted)
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.EntryNumber)
            .ToListAsync(cancellationToken);
        var journalIds = journalEntries.Select(item => item.Id).ToList();
        var journalLines = journalIds.Count == 0
            ? new List<FinalAccountsJournalLine>()
            : await db.FinalAccountsJournalLines.AsNoTracking().Where(item => journalIds.Contains(item.JournalEntryId)).ToListAsync(cancellationToken);
        var mappings = BuildMappingRows(profile, groups, accounts);
        var debit = FinalAccountsExchangeRules.RoundAmount(journalLines.Sum(item => item.Debit));
        var credit = FinalAccountsExchangeRules.RoundAmount(journalLines.Sum(item => item.Credit));
        var exceptions = BuildTallyExceptions(profile, accounts, journalEntries, debit, credit);
        var vouchers = journalEntries.Select(entry =>
        {
            var amount = journalLines.Where(line => line.JournalEntryId == entry.Id).Sum(line => line.Debit);
            return new TallyVoucherFixtureRow(entry.EntryNumber, entry.OnDate, MapVoucherType(profile, entry.SourceType), entry.Narration, amount);
        }).ToList();
        var xml = FinalAccountsExchangeRules.BuildTallyXmlFixture(profile.TestCompanyName, mappings, vouchers);
        var json = FinalAccountsExchangeRules.BuildJsonFixture(new
        {
            profile = profile.ProfileCode,
            release = profile.TallyRelease,
            company = profile.TestCompanyName,
            policy = profile.DuplicatePolicy.ToString(),
            directPosting = FinalAccountsExchangeRules.NoDirectTallyPosting,
            from,
            to,
            asOf,
            masters = mappings,
            vouchers
        });

        return new FinalAccountsExchangePreviewResponse(
            "TallyExchange",
            from,
            to,
            asOf,
            FinalAccountsExchangeRules.NormalizeFormat(format),
            false,
            profile.DuplicatePolicy.ToString(),
            groups.Count + accounts.Count,
            journalEntries.Count,
            debit,
            credit,
            FinalAccountsExchangeRules.RoundAmount(debit - credit),
            mappings,
            exceptions,
            xml,
            json,
            Array.Empty<FinalAccountsCaPackageItemDto>());
    }

    private async Task<FinalAccountsExchangeRun> CreateRunAsync(
        FinalAccountsScopeDto scope,
        FinalAccountsExchangeRunKind kind,
        Guid? profileId,
        DateTime from,
        DateTime to,
        DateTime asOf,
        FinalAccountsExchangePreviewResponse preview,
        string? format,
        string? notes,
        string actor,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var run = new FinalAccountsExchangeRun
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            RunNumber = await NextRunNumberAsync(scope, kind, to, cancellationToken),
            RunKind = kind,
            Status = FinalAccountsExchangeRunStatus.Previewed,
            TallyProfileId = profileId,
            PeriodFrom = from,
            PeriodTo = to,
            AsOf = asOf,
            Format = FinalAccountsExchangeRules.NormalizeFormat(format),
            MasterCount = preview.MasterCount,
            VoucherCount = preview.VoucherCount,
            ExceptionCount = preview.Exceptions.Count,
            ControlDebit = preview.ControlDebit,
            ControlCredit = preview.ControlCredit,
            PayloadHash = FinalAccountsExchangeRules.Sha256Hex(preview.XmlFixture + preview.JsonFixture + preview.ControlDebit + preview.ControlCredit),
            Notes = FinalAccountsExchangeRules.OptionalText(notes, 1000),
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        db.FinalAccountsExchangeRuns.Add(run);
        foreach (var exception in preview.Exceptions)
        {
            db.FinalAccountsExchangeExceptions.Add(new FinalAccountsExchangeException
            {
                ExchangeRunId = run.Id,
                Severity = exception.Severity,
                Code = exception.Code,
                Message = exception.Message,
                SourceType = exception.SourceType,
                SourceId = exception.SourceId,
                Revision = 1
            });
        }

        AddAudit(context, "CreateExchangeRun", nameof(FinalAccountsExchangeRun), run.Id, run.RunNumber, scope, actor, null, new { run.RunKind, run.Format, run.ExceptionCount });
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    private async Task<IReadOnlyList<PackageFile>> BuildCaPackageItemsAsync(
        FinalAccountsScopeDto scope,
        FinalAccountsCaPackageRequest request,
        HttpContext context,
        bool includeBytes,
        CancellationToken cancellationToken)
    {
        var query = new FinalAccountsCatalogQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId);
        var items = new List<PackageFile>();
        async Task AddReport(string path, string category, string description, Func<Task<FinalAccountsReportExport>> factory)
        {
            var export = includeBytes ? await factory() : new FinalAccountsReportExport(Path.GetFileName(path), "text/csv", Encoding.UTF8.GetBytes(description));
            items.Add(PackageFile.From(path, category, description, export.ContentType, export.Content));
        }

        await AddReport("reports/trial-balance.csv", "Trial Balance", "Trial Balance for CA review.", () => reports.ExportTrialBalanceAsync(new FinalAccountsTrialBalanceReportQuery(query.CompanyId, query.StoreGroupId, query.StoreId, request.From.Date, request.To.Date, "Group", true, "Monthly"), "csv", context, cancellationToken));
        await AddReport("reports/general-ledger.csv", "General Ledger", "General Ledger detail for CA review.", () => reports.ExportGeneralLedgerAsync(new FinalAccountsGeneralLedgerReportQuery(query.CompanyId, query.StoreGroupId, query.StoreId, null, request.From.Date, request.To.Date, false, 1, 5000), "csv", context, cancellationToken));
        await AddReport("reports/profit-loss.csv", "P&L", "Profit and Loss statement.", () => reports.ExportProfitLossAsync(new FinalAccountsProfitLossReportQuery(query.CompanyId, query.StoreGroupId, query.StoreId, request.From.Date, request.To.Date, "vertical", "None", false), "csv", context, cancellationToken));
        await AddReport("reports/balance-sheet.csv", "Balance Sheet", "Balance Sheet statement.", () => reports.ExportBalanceSheetAsync(new FinalAccountsBalanceSheetReportQuery(query.CompanyId, query.StoreGroupId, query.StoreId, request.AsOf.Date, null, request.EntityType, "None", false), "csv", context, cancellationToken));
        await AddReport("reports/cash-flow.csv", "Cash Flow", "Cash Flow statement.", () => reports.ExportCashFlowAsync(new FinalAccountsCashFlowReportQuery(query.CompanyId, query.StoreGroupId, query.StoreId, request.From.Date, request.To.Date, "None"), "csv", context, cancellationToken));

        foreach (var schedule in new[] { "debtors", "creditors", "inventory", "fixed-assets", "cash-bank", "gst-tds", "payroll" })
        {
            await AddReport($"schedules/{schedule}.csv", schedule, $"{schedule} schedule.", () => reports.ExportSchedulesAsync(new FinalAccountsSchedulesReportQuery(query.CompanyId, query.StoreGroupId, query.StoreId, request.AsOf.Date, schedule, true), "csv", context, cancellationToken));
        }

        items.Add(PackageFile.From("reports/adjustments.csv", "Adjustments", "CA adjustment batches and status.", "text/csv", Encoding.UTF8.GetBytes(await BuildAdjustmentsCsvAsync(scope, request.From.Date, request.To.Date, cancellationToken))));
        items.Add(PackageFile.From("reports/ratios.csv", "Ratios", "Core review ratios.", "text/csv", Encoding.UTF8.GetBytes(await BuildRatiosCsvAsync(query, request, context, cancellationToken))));
        items.Add(PackageFile.From("controls/source-control-totals.csv", "Source Control Totals", "Journal source control totals.", "text/csv", Encoding.UTF8.GetBytes(await BuildSourceControlsCsvAsync(scope, request.From.Date, request.To.Date, cancellationToken))));
        items.Add(PackageFile.From("controls/exception-report.csv", "Exception Report", "Known export and package exceptions.", "text/csv", Encoding.UTF8.GetBytes(BuildExceptionsCsv(BuildPackageExceptions(items)))));
        items.Add(PackageFile.From("supporting-documents/manifest.csv", "Supporting Documents", request.IncludeSupportingDocuments ? "Supporting document placeholders for CA review." : "Supporting documents were not embedded in BS-12.", "text/csv", Encoding.UTF8.GetBytes("Path,Status,Note\nsupporting-documents/,Placeholder,Attach external source documents outside source control\n")));
        items.Add(PackageFile.From("README.md", "README", "CA package README.", "text/markdown", Encoding.UTF8.GetBytes(BuildCaReadme(request, items))));
        return items;
    }

    private static IReadOnlyList<FinalAccountsExchangeMappingRowDto> BuildMappingRows(
        FinalAccountsTallyProfile profile,
        IReadOnlyList<FinalAccountsAccountGroup> groups,
        IReadOnlyList<FinalAccountsAccount> accounts)
    {
        var groupMapping = FinalAccountsExchangeRules.FromJson(profile.GroupMappingJson);
        var ledgerMapping = FinalAccountsExchangeRules.FromJson(profile.LedgerMappingJson);
        var rows = new List<FinalAccountsExchangeMappingRowDto>();
        rows.AddRange(groups.Select(item => new FinalAccountsExchangeMappingRowDto("AccountGroup", item.Code, item.Name, groupMapping.GetValueOrDefault(item.Code, item.Name), groupMapping.ContainsKey(item.Code) ? "Mapped" : "Default")));
        rows.AddRange(accounts.Select(item => new FinalAccountsExchangeMappingRowDto("Ledger", item.Code, item.Name, ledgerMapping.GetValueOrDefault(item.Code, item.Name), ledgerMapping.ContainsKey(item.Code) ? "Mapped" : "Default")));
        rows.Add(new FinalAccountsExchangeMappingRowDto("VoucherType", "Journal", "Final Accounts Journal", MapVoucherType(profile, "Journal"), "Mapped"));
        rows.Add(new FinalAccountsExchangeMappingRowDto("Tax", "GST", "GST/TDS mapping", "Duties & Taxes", "Default"));
        rows.Add(new FinalAccountsExchangeMappingRowDto("StockCostCentre", "STORE", "Store cost centre", "Garmetix Stores", "Default"));
        return rows;
    }

    private static string MapVoucherType(FinalAccountsTallyProfile profile, string? sourceType)
    {
        var mapping = FinalAccountsExchangeRules.FromJson(profile.VoucherTypeMappingJson);
        var key = string.IsNullOrWhiteSpace(sourceType) ? "Journal" : sourceType;
        return mapping.GetValueOrDefault(key, key.Contains("Payment", StringComparison.OrdinalIgnoreCase) ? "Payment" : key.Contains("Receipt", StringComparison.OrdinalIgnoreCase) ? "Receipt" : "Journal");
    }

    private static IReadOnlyList<FinalAccountsExchangeExceptionDto> BuildTallyExceptions(
        FinalAccountsTallyProfile profile,
        IReadOnlyList<FinalAccountsAccount> accounts,
        IReadOnlyList<FinalAccountsJournalEntry> journals,
        decimal debit,
        decimal credit)
    {
        var exceptions = new List<FinalAccountsExchangeExceptionDto>();
        if (profile.DirectPostingAllowed)
        {
            exceptions.Add(new("Error", FinalAccountsExchangeRules.NoDirectTallyPosting, "Direct production Tally posting is not allowed by Final Accounts exchange.", nameof(FinalAccountsTallyProfile), profile.Id));
        }

        if (accounts.Count == 0)
        {
            exceptions.Add(new("Warning", "NO_MASTERS", "No Final Accounts ledgers were found for this scope.", "Account", null));
        }

        if (journals.Count == 0)
        {
            exceptions.Add(new("Warning", "NO_TRANSACTIONS", "No posted Final Accounts journals were found for the selected period.", "Journal", null));
        }

        if (FinalAccountsExchangeRules.RoundAmount(debit - credit) != 0m)
        {
            exceptions.Add(new("Error", "CONTROL_TOTAL_MISMATCH", "Tally transaction export debit and credit controls are not equal.", "Journal", null));
        }

        return exceptions;
    }

    private static IReadOnlyList<FinalAccountsExchangeExceptionDto> BuildPackageExceptions(IReadOnlyList<PackageFile> items)
    {
        var exceptions = new List<FinalAccountsExchangeExceptionDto>();
        if (items.Count == 0)
        {
            exceptions.Add(new("Error", "EMPTY_PACKAGE", "CA package contains no files.", "Package", null));
        }

        if (items.Any(item => item.Bytes.Length == 0))
        {
            exceptions.Add(new("Warning", "EMPTY_PACKAGE_ITEM", "One or more CA package files are empty.", "Package", null));
        }

        return exceptions;
    }

    private async Task<FinalAccountsTallyProfile> ResolveProfileAsync(FinalAccountsScopeDto scope, Guid? profileId, CancellationToken cancellationToken)
    {
        var query = ProfilesInScope(scope);
        var profile = profileId.HasValue
            ? await query.FirstOrDefaultAsync(item => item.Id == profileId.Value, cancellationToken)
            : await query.Where(item => item.IsActive).OrderBy(item => item.ProfileCode).FirstOrDefaultAsync(cancellationToken);
        return profile ?? throw new KeyNotFoundException("Create an active Tally profile before running exchange preview/export.");
    }

    private async Task<string> NextRunNumberAsync(FinalAccountsScopeDto scope, FinalAccountsExchangeRunKind kind, DateTime periodEnd, CancellationToken cancellationToken)
    {
        var prefix = FinalAccountsExchangeRules.BuildRunNumberPrefix(kind, periodEnd);
        var count = await db.FinalAccountsExchangeRuns.CountAsync(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId && item.RunNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}-{count + 1:0000}";
    }

    private static byte[] BuildTallyZip(string runNumber, FinalAccountsTallyProfile profile, FinalAccountsExchangePreviewResponse preview)
    {
        var files = new[]
        {
            PackageFile.From("tally/masters-and-vouchers.xml", "Tally XML", "TallyPrime XML fixture.", "application/xml", Encoding.UTF8.GetBytes(preview.XmlFixture)),
            PackageFile.From("tally/masters-and-vouchers.json", "Tally JSON", "TallyPrime JSON fixture.", "application/json", Encoding.UTF8.GetBytes(preview.JsonFixture)),
            PackageFile.From("tally/preview.csv", "Preview", "Tally exchange preview.", "text/csv", Encoding.UTF8.GetBytes(BuildPreviewCsv(preview))),
            PackageFile.From("tally/exceptions.csv", "Exceptions", "Tally exchange exceptions.", "text/csv", Encoding.UTF8.GetBytes(BuildExceptionsCsv(preview.Exceptions))),
            PackageFile.From("README.md", "README", "Tally exchange README.", "text/markdown", Encoding.UTF8.GetBytes($"# {runNumber} TallyPrime Exchange\n\nProfile: {profile.ProfileCode}\nRelease: {profile.TallyRelease}\nCompany: {profile.TestCompanyName}\nPolicy: {profile.DuplicatePolicy}\nDirect posting: disabled\n"))
        };
        return BuildZip(files);
    }

    private static byte[] BuildCaPackageZip(string runNumber, FinalAccountsCaPackageRequest request, IReadOnlyList<PackageFile> items)
    {
        var manifest = new FinalAccountsCaPackageManifestDto(runNumber, DateTime.UtcNow, request.From.Date, request.To.Date, request.AsOf.Date, string.Empty, items.Select(item => item.Dto).ToList());
        var allItems = items.Concat(new[]
        {
            PackageFile.From("manifest.json", "Manifest", "Package manifest.", "application/json", Encoding.UTF8.GetBytes(FinalAccountsExchangeRules.BuildJsonFixture(manifest))),
            PackageFile.From("checksums.sha256", "Checksum", "Per-file checksums.", "text/plain", Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, items.Select(item => $"{item.Dto.Sha256}  {item.Dto.Path}")) + Environment.NewLine))
        }).ToList();
        return BuildZip(allItems);
    }

    private static byte[] BuildZip(IEnumerable<PackageFile> files)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = zip.CreateEntry(file.Dto.Path, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(file.Bytes, 0, file.Bytes.Length);
            }
        }

        return stream.ToArray();
    }

    private static string BuildPreviewCsv(FinalAccountsExchangePreviewResponse preview)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Type,SourceKey,SourceName,TallyName,Status");
        foreach (var row in preview.Mappings)
        {
            builder.AppendLine(string.Join(",", EscapeCsv(row.MappingType), EscapeCsv(row.SourceKey), EscapeCsv(row.SourceName), EscapeCsv(row.TallyName), EscapeCsv(row.Status)));
        }

        builder.AppendLine();
        builder.AppendLine("Metric,Value");
        builder.AppendLine($"MasterCount,{preview.MasterCount}");
        builder.AppendLine($"VoucherCount,{preview.VoucherCount}");
        builder.AppendLine($"ControlDebit,{preview.ControlDebit}");
        builder.AppendLine($"ControlCredit,{preview.ControlCredit}");
        builder.AppendLine($"ControlDifference,{preview.ControlDifference}");
        builder.AppendLine($"DuplicatePolicy,{preview.DuplicatePolicy}");
        builder.AppendLine("DirectPosting,Disabled");
        return builder.ToString();
    }

    private static string BuildExceptionsCsv(IReadOnlyList<FinalAccountsExchangeExceptionDto> exceptions)
    {
        var builder = new StringBuilder("Severity,Code,Message,SourceType,SourceId\n");
        foreach (var item in exceptions)
        {
            builder.AppendLine(string.Join(",", EscapeCsv(item.Severity), EscapeCsv(item.Code), EscapeCsv(item.Message), EscapeCsv(item.SourceType ?? string.Empty), item.SourceId?.ToString() ?? string.Empty));
        }

        return builder.ToString();
    }

    private async Task<string> BuildAdjustmentsCsvAsync(FinalAccountsScopeDto scope, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await db.FinalAccountsAdjustmentBatches.AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId && item.AdjustmentDate >= from && item.AdjustmentDate <= to)
            .OrderBy(item => item.AdjustmentDate)
            .ToListAsync(cancellationToken);
        var builder = new StringBuilder("Batch,Date,Title,Status,Journal,Reference\n");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", EscapeCsv(row.BatchNumber), row.AdjustmentDate.ToString("yyyy-MM-dd"), EscapeCsv(row.Title), row.Status, row.JournalEntryId, EscapeCsv(row.ReferenceNumber ?? string.Empty)));
        }

        return builder.ToString();
    }

    private async Task<string> BuildSourceControlsCsvAsync(FinalAccountsScopeDto scope, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await db.FinalAccountsJournalEntries.AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId && item.OnDate >= from && item.OnDate <= to && item.Status == FinalAccountsJournalStatus.Posted)
            .GroupJoin(db.FinalAccountsJournalLines.AsNoTracking(), entry => entry.Id, line => line.JournalEntryId, (entry, lines) => new { entry.SourceType, Debit = lines.Sum(line => line.Debit), Credit = lines.Sum(line => line.Credit), Count = 1 })
            .GroupBy(item => item.SourceType)
            .Select(group => new { SourceType = group.Key, Count = group.Sum(item => item.Count), Debit = group.Sum(item => item.Debit), Credit = group.Sum(item => item.Credit) })
            .ToListAsync(cancellationToken);
        var builder = new StringBuilder("SourceType,Count,Debit,Credit,Difference\n");
        foreach (var row in rows)
        {
            builder.AppendLine($"{EscapeCsv(row.SourceType)},{row.Count},{row.Debit},{row.Credit},{FinalAccountsExchangeRules.RoundAmount(row.Debit - row.Credit)}");
        }

        return builder.ToString();
    }

    private async Task<string> BuildRatiosCsvAsync(FinalAccountsCatalogQuery query, FinalAccountsCaPackageRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var profit = await reports.GetProfitLossAsync(new FinalAccountsProfitLossReportQuery(query.CompanyId, query.StoreGroupId, query.StoreId, request.From.Date, request.To.Date, "vertical", "None", false), context, cancellationToken);
        var balance = await reports.GetBalanceSheetAsync(new FinalAccountsBalanceSheetReportQuery(query.CompanyId, query.StoreGroupId, query.StoreId, request.AsOf.Date, null, request.EntityType, "None", false), context, cancellationToken);
        var builder = new StringBuilder("Ratio,Value\n");
        builder.AppendLine($"GrossMarginPercent,{Percent(profit.GrossProfit, profit.Revenue)}");
        builder.AppendLine($"NetProfitPercent,{Percent(profit.ProfitAfterTax, profit.Revenue)}");
        builder.AppendLine($"DebtToEquity,{Ratio(balance.TotalLiabilities, balance.TotalEquity)}");
        builder.AppendLine($"BalanceDifference,{balance.Difference}");
        return builder.ToString();
    }

    private static string BuildCaReadme(FinalAccountsCaPackageRequest request, IReadOnlyList<PackageFile> items)
        => $"# Final Accounts CA Package\n\nPeriod: {request.From:yyyy-MM-dd} to {request.To:yyyy-MM-dd}\nAs of: {request.AsOf:yyyy-MM-dd}\nFiles: {items.Count}\n\nThis package is read-only evidence for CA review. It does not post to Tally or production systems.\n";

    private IQueryable<FinalAccountsTallyProfile> ProfilesInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsTallyProfiles.Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);

    private static FinalAccountsTallyProfileDto ToProfileDto(FinalAccountsTallyProfile item)
        => new(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.ProfileCode,
            item.Name,
            item.TallyRelease,
            item.TestCompanyName,
            item.BaseCurrency,
            item.Country,
            item.GstRegistrationType,
            item.DuplicatePolicy.ToString(),
            item.DirectPostingAllowed,
            FinalAccountsExchangeRules.FromJson(item.GroupMappingJson),
            FinalAccountsExchangeRules.FromJson(item.LedgerMappingJson),
            FinalAccountsExchangeRules.FromJson(item.VoucherTypeMappingJson),
            FinalAccountsExchangeRules.FromJson(item.TaxMappingJson),
            FinalAccountsExchangeRules.FromJson(item.StockCostCentreMappingJson),
            item.IsActive,
            item.Notes,
            item.Revision);

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, Guid? companyId, Guid? storeGroupId, Guid? storeId)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return new FinalAccountsScopeDto(companyId, storeGroupId, storeId);
        }

        return new FinalAccountsScopeDto(
            WorkspaceScope.ClaimGuid(context, "companyId"),
            WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            WorkspaceScope.ClaimGuid(context, "storeId"));
    }

    private static void EnsureCanWrite(object entity, HttpContext context)
    {
        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            throw new InvalidOperationException(message ?? "Selected scope is outside your access.");
        }
    }

    private static string ResolveActor(HttpContext context)
        => context.User.FindFirstValue(ClaimTypes.Name)
            ?? context.User.FindFirstValue("name")
            ?? context.User.FindFirstValue(ClaimTypes.Email)
            ?? "system";

    private void AddAudit(HttpContext context, string action, string entityName, Guid entityId, string reference, FinalAccountsScopeDto scope, string actor, object? before, object? after)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            OccurredAt = DateTime.UtcNow,
            Action = action,
            Module = "Final Accounts",
            EntityName = entityName,
            EntityDisplayName = reference,
            EntityId = entityId,
            Reference = reference,
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            UserName = actor,
            Source = "FinalAccountsExchange",
            RequestMethod = context.Request.Method,
            RequestPath = context.Request.Path,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            ChangesJson = JsonSerializer.Serialize(new { action }),
            ChangedFieldCount = 1,
            TraceIdentifier = context.TraceIdentifier
        });
    }

    private static decimal Percent(decimal numerator, decimal denominator)
        => denominator == 0m ? 0m : FinalAccountsExchangeRules.RoundAmount(numerator / denominator * 100m);

    private static decimal Ratio(decimal numerator, decimal denominator)
        => denominator == 0m ? 0m : Math.Round(numerator / denominator, 4, MidpointRounding.AwayFromZero);

    private static string EscapeCsv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private sealed record PackageFile(FinalAccountsCaPackageItemDto Dto, byte[] Bytes)
    {
        public static PackageFile From(string path, string category, string description, string contentType, byte[] bytes)
            => new(new FinalAccountsCaPackageItemDto(path, category, description, contentType, bytes.Length, FinalAccountsExchangeRules.Sha256Hex(bytes)), bytes);
    }
}
