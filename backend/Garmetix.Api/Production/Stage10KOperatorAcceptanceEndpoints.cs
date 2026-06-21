using Garmetix.Api.AppInfo;
using Garmetix.Api.Auth;

namespace Garmetix.Api.Production;

public static class Stage10KOperatorAcceptanceEndpoints
{
    public static RouteGroupBuilder MapStage10KOperatorAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stage10k/operator-acceptance")
            .WithTags("Stage 10K Operator Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("", Summary);
        group.MapGet("/checklist", Checklist);

        return group;
    }

    private static IResult Summary(ILoggerFactory loggerFactory)
    {
        try
        {
            var sections = BuildSections();
            var requiredItemCount = sections.Where(section => section.Required).Sum(section => section.Items.Count);
            var dailyRoutes = sections
                .Select(section => section.Route)
                .Where(route => !string.IsNullOrWhiteSpace(route))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                stage = AppInfoEndpoints.Stage,
                releaseName = AppInfoEndpoints.ReleaseName,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                overallStatus = "Ready for operator rehearsal",
                requiredItemCount,
                sectionCount = sections.Length,
                dailyRoutes,
                sections,
                warning = (string?)null
            });
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Stage10KOperatorAcceptance").LogError(ex, "Stage 10K operator acceptance summary failed; returning safe degraded payload.");
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                stage = AppInfoEndpoints.Stage,
                releaseName = AppInfoEndpoints.ReleaseName,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                overallStatus = "Degraded - review logs",
                requiredItemCount = 0,
                sectionCount = 0,
                dailyRoutes = Array.Empty<string>(),
                sections = Array.Empty<Stage10KOperatorSection>(),
                warning = ex.Message
            });
        }
    }

    private static IResult Checklist(ILoggerFactory loggerFactory)
    {
        try
        {
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                items = BuildSections().SelectMany(section => section.Items.Select(item => new
                {
                    section = section.Title,
                    sectionRoute = section.Route,
                    cadence = section.Cadence,
                    required = section.Required,
                    item
                }))
            });
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Stage10KOperatorAcceptance").LogError(ex, "Stage 10K operator acceptance checklist failed; returning safe degraded payload.");
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                error = ex.Message,
                items = Array.Empty<object>()
            });
        }
    }

    private static Stage10KOperatorSection[] BuildSections() =>
    [
        new("Day opening and store readiness", "Every store day", "/store-day", true,
        [
            "Open Store Operations before billing and confirm the selected working store.",
            "Complete Day Open with cash opening, operator and remarks captured.",
            "Confirm API Service is live and Message Logs do not show blocking startup errors."
        ],
        [
            "Day Open number",
            "Store name",
            "API status"
        ]),
        new("Billing and sales desk", "Every sale shift", "/billing/new", true,
        [
            "Create a Sale Invoice from the dedicated full-page invoice screen.",
            "Use Save & Print after confirming customer, salesman, payment split, GST or non-GST mode and totals.",
            "Scan the printed QR or barcode from Document Scanner and verify it opens the saved invoice."
        ],
        [
            "Invoice number",
            "Printed PDF",
            "QR scan result"
        ]),
        new("Cash closing and petty cash", "Daily closing", "/petty-cash", true,
        [
            "Verify Petty Cash Sheet opening balance comes from the previous closing balance.",
            "Review pre-calculated income, payment and cash-in-hand before saving.",
            "Print the color A5 sheet and confirm mismatch alerts are visible in Message Logs when values differ."
        ],
        [
            "Petty cash sheet number",
            "A5 print",
            "Cash-in-hand value"
        ]),
        new("Purchase and inventory", "Daily or inward day", "/purchase/new", true,
        [
            "Post purchase inward with product, HSN, GST, quantity, rate and vendor snapshots.",
            "Review Stock Reports for low-stock, ageing and ledger mismatch indicators.",
            "Use Import/Export only through validate-then-commit and retain downloaded error reports."
        ],
        [
            "Purchase number",
            "Stock movement",
            "Import report"
        ]),
        new("Voucher, accounting and banking", "Daily accounting", "/vouchers", true,
        [
            "Create accounting vouchers with ledger selection only; party-ledger flags remain internal.",
            "For non-cash payment or receipt, select the correct bank account so bank transaction audit is created.",
            "Review cheque issue/deposit logs with purpose, party, reference and clearance state."
        ],
        [
            "Voucher number",
            "Bank transaction",
            "Cheque log"
        ]),
        new("HR attendance and payroll", "Daily and monthly", "/attendance/today", true,
        [
            "Capture or import daily attendance and review exceptions before monthly generation.",
            "Generate monthly attendance on month close and review payroll-ready status.",
            "Generate payslips, salary payment slips and PDF/share output only after operator approval."
        ],
        [
            "Attendance date",
            "Monthly summary",
            "Payslip/payment slip"
        ]),
        new("Backup, restore and support", "Daily and before updates", "/backup-maintenance", true,
        [
            "Confirm the latest backup and disposable restore drill are healthy before package updates.",
            "Review System Health, Runtime Diagnostics and Production Readiness after every deployment.",
            "When any save or print fails, capture visible message, version/build code and Message Logs entry."
        ],
        [
            "Backup file",
            "Restore drill marker",
            "Message log id"
        ])
    ];

    private sealed record Stage10KOperatorSection(
        string Title,
        string Cadence,
        string Route,
        bool Required,
        IReadOnlyList<string> Items,
        IReadOnlyList<string> Evidence);
}
