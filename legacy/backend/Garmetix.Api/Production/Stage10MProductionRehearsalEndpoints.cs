using Garmetix.Api.AppInfo;
using Garmetix.Api.Auth;

namespace Garmetix.Api.Production;

public static class Stage10MProductionRehearsalEndpoints
{
    public static RouteGroupBuilder MapStage10MProductionRehearsalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stage10m/production-rehearsal")
            .WithTags("Stage 10M Production Rehearsal")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("", Summary);
        group.MapGet("/run-sheet", RunSheet);

        return group;
    }

    private static IResult Summary(ILoggerFactory loggerFactory)
    {
        try
        {
            var phases = BuildPhases();
            var blockingCount = phases.Sum(phase => phase.BlockingChecks.Count);

            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                stage = AppInfoEndpoints.Stage,
                releaseName = AppInfoEndpoints.ReleaseName,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                overallStatus = "Ready for live-data rehearsal",
                rehearsalMode = "Use a real store-day dataset before mobile/device work",
                phaseCount = phases.Length,
                blockingCount,
                phases,
                issueBuckets = BuildIssueBuckets(),
                warning = (string?)null
            });
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Stage10MProductionRehearsal").LogError(ex, "Stage 10M production rehearsal summary failed; returning safe degraded payload.");
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                stage = AppInfoEndpoints.Stage,
                releaseName = AppInfoEndpoints.ReleaseName,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                overallStatus = "Degraded - review logs",
                rehearsalMode = "Unavailable",
                phaseCount = 0,
                blockingCount = 0,
                phases = Array.Empty<Stage10MRehearsalPhase>(),
                issueBuckets = Array.Empty<Stage10MIssueBucket>(),
                warning = ex.Message
            });
        }
    }

    private static IResult RunSheet(ILoggerFactory loggerFactory)
    {
        try
        {
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                items = BuildPhases().SelectMany(phase => phase.Actions.Select(action => new
                {
                    phase = phase.Title,
                    route = phase.Route,
                    expectedEvidence = phase.ExpectedEvidence,
                    action
                }))
            });
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Stage10MProductionRehearsal").LogError(ex, "Stage 10M production rehearsal run sheet failed; returning safe degraded payload.");
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

    private static Stage10MRehearsalPhase[] BuildPhases() =>
    [
        new("Pre-flight and workspace", "/production-readiness", "API health, selected store, version/build and backup health",
        [
            "Confirm the current package version and build code in About/System Info.",
            "Confirm the working store and role are correct before creating operational data.",
            "Confirm latest backup and restore-drill marker before the rehearsal begins."
        ],
        [
            "API health must be live.",
            "Selected store must match the physical store.",
            "Latest backup must be usable before any live-data rehearsal."
        ]),
        new("Store day and cash opening", "/store-day", "Day Open number, opening cash and operator",
        [
            "Open the store day using the real opening cash balance.",
            "Compare petty cash previous closing with today's opening.",
            "Record any mismatch and owner confirmation in Message Logs."
        ],
        [
            "Day Open must save without duplicate day or date shift.",
            "Opening balance must not silently override user-confirmed value."
        ]),
        new("Billing and document scan", "/billing/new", "Invoice number, print, QR scan and payment split",
        [
            "Create one cash sale, one non-cash sale and one mixed-payment sale.",
            "Use Save & Print and scan the generated QR or barcode.",
            "Confirm the invoice opens from Document Scanner and totals match the PDF."
        ],
        [
            "Sale invoice save must not fail for missing salesman.",
            "Print/download must use hosted-safe URLs.",
            "QR scan must open the permitted document."
        ]),
        new("Purchase, stock and import/export", "/import-export", "Purchase inward, stock report and validated import output",
        [
            "Post a purchase inward for an existing product and a new product.",
            "Review Stock Reports for quantity, valuation and risk band.",
            "Validate one import template and commit only if no row errors exist."
        ],
        [
            "Stock movement must be visible after purchase.",
            "Import commit must remain blocked when validation errors exist."
        ]),
        new("Accounting, banking and voucher print", "/vouchers", "Voucher number, ledger, bank transaction and PDF",
        [
            "Create one cash voucher, one bank payment and one receipt voucher.",
            "Confirm party/bank ledgers are internal and not exposed as user flags.",
            "Download and print voucher PDF, then check audit links."
        ],
        [
            "Non-cash voucher must require a bank account.",
            "Voucher print must be create-only unless explicitly reprinted."
        ]),
        new("HR, attendance and payroll", "/attendance/today", "Attendance review, monthly summary, payslip and salary payment slip",
        [
            "Record attendance for at least one employee and review exceptions.",
            "Review monthly attendance readiness and salary draft impact.",
            "Generate payslip/payment slip only after operator approval."
        ],
        [
            "Salary payment preview must include advance deduction and previous due.",
            "Payslip and salary payment PDF download must work from hosted deployment."
        ]),
        new("Close, support and go/no-go", "/production-support", "Message Logs, support drill result and final decision",
        [
            "Close petty cash and compare cash in hand with counted cash.",
            "Run Production Support drill for every failed save, print, backup, email or hosted API issue.",
            "Mark rehearsal go/no-go only after all blocking checks are closed or accepted by owner."
        ],
        [
            "No unresolved save, print, access or hosted URL issue can remain before Stage 11.",
            "Every exception must have a Message Log id or written evidence."
        ])
    ];

    private static Stage10MIssueBucket[] BuildIssueBuckets() =>
    [
        new("Save or validation failure", "/message-logs", "Capture visible message, payload context, Message Log id and whether a record was created."),
        new("Print or QR failure", "/print-final-acceptance", "Capture document number, PDF download result, QR scan result and printer/browser state."),
        new("Access or role mismatch", "/access", "Capture user, role, route, expected right and denied action."),
        new("Hosted API or tunnel mismatch", "/runtime-diagnostics", "Capture public URL, API origin, forwarded host/proto and failing endpoint."),
        new("Accounting or ledger mismatch", "/vouchers", "Capture ledger, party/bank link, voucher number, journal number and affected report.")
    ];

    private sealed record Stage10MRehearsalPhase(
        string Title,
        string Route,
        string ExpectedEvidence,
        IReadOnlyList<string> Actions,
        IReadOnlyList<string> BlockingChecks);

    private sealed record Stage10MIssueBucket(
        string Title,
        string Route,
        string Evidence);
}
