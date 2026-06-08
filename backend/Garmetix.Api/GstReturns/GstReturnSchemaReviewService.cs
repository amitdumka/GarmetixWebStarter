using System.Globalization;

namespace Garmetix.Api.GstReturns;

public sealed record GstSchemaReviewItem(string Section, string ExportKey, string Status, string Notes);

public sealed record GstSchemaReviewResponse(
    string ReviewedOnUtc,
    string ReviewBasis,
    bool ManualPortalValidationRequired,
    IReadOnlyList<string> ProductionWarnings,
    IReadOnlyList<GstSchemaReviewItem> Gstr1,
    IReadOnlyList<GstSchemaReviewItem> Gstr3B);

public static class GstReturnSchemaReviewService
{
    public static GstSchemaReviewResponse Build()
    {
        var reviewedOn = DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture);
        var warnings = new[]
        {
            "This module is still standalone/manual and is not linked to Billing or Purchase.",
            "Generated JSON/Excel follows commonly used GST offline-utility key groups, but final upload must be tested with the GST portal/offline utility before production filing.",
            "Keep a CA/accountant review step enabled until live portal acceptance is confirmed for your GSTIN and return period.",
            "GSTR-3B liability values may be auto-populated/locked by GSTN policy changes; reconcile GSTR-1/GSTR-1A before filing."
        };

        var gstr1 = new[]
        {
            Item("Header", "gstin, fp, gt, cur_gt", "Reviewed", "GSTIN, return period, gross turnover and current turnover are emitted at root level."),
            Item("B2B invoices", "b2b[].ctin, inv[], itms[].itm_det", "Reviewed", "Recipient-wise invoice grouping and tax details are emitted with invoice date dd-MM-yyyy."),
            Item("B2C summary", "b2cs[]", "Reviewed", "Rate/POS-wise B2C taxable value and tax totals are emitted."),
            Item("HSN summary", "hsn.data[]", "Reviewed", "HSN, UQC, quantity, total value, taxable value and tax details are emitted."),
            Item("Nil/exempt/non-GST", "nil.inv[]", "Reviewed", "Nil-rated, exempt and non-GST amounts are emitted."),
            Item("Documents issued", "doc_issue.doc_det[]", "Reviewed", "Document serial range, total/cancelled/net issued values are emitted."),
            Item("Excel workbook", "Return Summary, B2B, B2CS, HSN, Documents, Nil Exempt Non-GST, Portal Review Checklist", "Reviewed", "Excel export includes manual entry sheets plus a review checklist sheet.")
        };

        var gstr3b = new[]
        {
            Item("Header", "gstin, ret_period", "Reviewed", "GSTIN and return period are emitted at root level."),
            Item("3.1 Supplies", "sup_details", "Reviewed", "Outward taxable, zero-rated, nil/exempt, reverse-charge and non-GST blocks are emitted."),
            Item("3.2 Interstate", "inter_sup", "Reviewed", "Unregistered, composition and UIN interstate supply blocks are emitted."),
            Item("4 ITC", "itc_elg", "Reviewed", "Available, reversed, net and ineligible ITC groups are emitted."),
            Item("5 Inward", "inward_sup", "Reviewed", "Composition, nil-rated/exempt and non-GST inward summaries are emitted."),
            Item("Interest/Late fee", "intr_ltfee", "Reviewed", "Interest and late-fee blocks are emitted."),
            Item("Excel workbook", "Return Summary, 3.1 Supplies, 3.2 Interstate, 4 ITC, 5 Inward, Interest Late Fee, Portal Review Checklist", "Reviewed", "Excel export includes manual entry sheets plus a review checklist sheet.")
        };

        return new GstSchemaReviewResponse(
            reviewedOn,
            "Garmetix manual GST module review against common GST offline JSON key groups and portal-ready workbook sections.",
            ManualPortalValidationRequired: true,
            warnings,
            gstr1,
            gstr3b);
    }

    public static byte[] BuildExcel()
    {
        var review = Build();
        var sheets = new List<XlsxSheet>
        {
            new("Review Summary", new[] { "Field", "Value" }, new[]
            {
                new[] { "Reviewed On UTC", review.ReviewedOnUtc },
                new[] { "Review Basis", review.ReviewBasis },
                new[] { "Manual Portal Validation Required", review.ManualPortalValidationRequired ? "Yes" : "No" },
                new[] { "Production Use", "Use only after GST portal/offline utility test upload passes for the target return period." }
            }),
            new("GSTR1 Mapping", new[] { "Section", "Export Key", "Status", "Notes" }, review.Gstr1.Select(item => new[] { item.Section, item.ExportKey, item.Status, item.Notes })),
            new("GSTR3B Mapping", new[] { "Section", "Export Key", "Status", "Notes" }, review.Gstr3B.Select(item => new[] { item.Section, item.ExportKey, item.Status, item.Notes })),
            new("Production Warnings", new[] { "No", "Warning" }, review.ProductionWarnings.Select((warning, index) => new[] { (index + 1).ToString(CultureInfo.InvariantCulture), warning }))
        };

        return SimpleXlsxBuilder.Build(sheets);
    }

    private static GstSchemaReviewItem Item(string section, string exportKey, string status, string notes) =>
        new(section, exportKey, status, notes);
}
