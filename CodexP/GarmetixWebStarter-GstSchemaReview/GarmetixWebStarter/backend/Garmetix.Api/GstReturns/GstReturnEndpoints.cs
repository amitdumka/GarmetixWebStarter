using Garmetix.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Garmetix.Api.GstReturns;

public static class GstReturnEndpoints
{
    public static RouteGroupBuilder MapGstReturnEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst-returns")
            .WithTags("GST Returns")
            .RequireAuthorization(GarmetixPolicies.Accounting);


        group.MapGet("/schema-review", () =>
            Results.Ok(GstReturnSchemaReviewService.Build()));

        group.MapGet("/schema-review/excel", () =>
        {
            var bytes = GstReturnSchemaReviewService.BuildExcel();
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Garmetix-GST-Schema-Review.xlsx");
        });

        group.MapPost("/gstr1/preview", ([FromBody] Gstr1ExportRequest request) =>
            Results.Ok(GstReturnExportService.PreviewGstr1(Normalize(request))));

        group.MapPost("/gstr1/json", ([FromBody] Gstr1ExportRequest request) =>
        {
            var normalized = Normalize(request);
            var issues = GstReturnExportService.PreviewGstr1(normalized).Issues;
            if (issues.Count > 0)
            {
                return Results.BadRequest(new { message = "Fix GST validation issues before export.", issues });
            }

            var bytes = GstReturnExportService.BuildGstr1Json(normalized);
            return Results.File(bytes, "application/json", FileName("GSTR1", normalized.Header, "json"));
        });

        group.MapPost("/gstr1/excel", ([FromBody] Gstr1ExportRequest request) =>
        {
            var normalized = Normalize(request);
            var issues = GstReturnExportService.PreviewGstr1(normalized).Issues;
            if (issues.Count > 0)
            {
                return Results.BadRequest(new { message = "Fix GST validation issues before export.", issues });
            }

            var bytes = GstReturnExportService.BuildGstr1Excel(normalized);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName("GSTR1", normalized.Header, "xlsx"));
        });

        group.MapPost("/gstr3b/preview", ([FromBody] Gstr3BExportRequest request) =>
            Results.Ok(GstReturnExportService.PreviewGstr3B(Normalize(request))));

        group.MapPost("/gstr3b/json", ([FromBody] Gstr3BExportRequest request) =>
        {
            var normalized = Normalize(request);
            var issues = GstReturnExportService.PreviewGstr3B(normalized).Issues;
            if (issues.Count > 0)
            {
                return Results.BadRequest(new { message = "Fix GST validation issues before export.", issues });
            }

            var bytes = GstReturnExportService.BuildGstr3BJson(normalized);
            return Results.File(bytes, "application/json", FileName("GSTR3B", normalized.Header, "json"));
        });

        group.MapPost("/gstr3b/excel", ([FromBody] Gstr3BExportRequest request) =>
        {
            var normalized = Normalize(request);
            var issues = GstReturnExportService.PreviewGstr3B(normalized).Issues;
            if (issues.Count > 0)
            {
                return Results.BadRequest(new { message = "Fix GST validation issues before export.", issues });
            }

            var bytes = GstReturnExportService.BuildGstr3BExcel(normalized);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName("GSTR3B", normalized.Header, "xlsx"));
        });

        return group;
    }

    private static Gstr1ExportRequest Normalize(Gstr1ExportRequest request) => request with
    {
        Header = Normalize(request.Header),
        B2BInvoices = request.B2BInvoices ?? [],
        B2CSummaries = request.B2CSummaries ?? [],
        HsnSummaries = request.HsnSummaries ?? [],
        DocumentsIssued = request.DocumentsIssued ?? [],
        NilRatedSupplies = request.NilRatedSupplies ?? []
    };

    private static Gstr3BExportRequest Normalize(Gstr3BExportRequest request) => request with
    {
        Header = Normalize(request.Header),
        Supplies = request.Supplies ?? EmptySupplies,
        InterStateSupplies = request.InterStateSupplies ?? EmptyInterState,
        Itc = request.Itc ?? EmptyItc,
        InwardSupplies = request.InwardSupplies ?? EmptyInward,
        InterestLateFee = request.InterestLateFee ?? EmptyFee
    };

    private static GstReturnPeriodRequest Normalize(GstReturnPeriodRequest? header) => header is null
        ? new GstReturnPeriodRequest(string.Empty, string.Empty, 0, 0, string.Empty, string.Empty)
        : header with
        {
            Gstin = (header.Gstin ?? string.Empty).Trim().ToUpperInvariant(),
            ReturnPeriod = (header.ReturnPeriod ?? string.Empty).Trim(),
            LegalName = (header.LegalName ?? string.Empty).Trim(),
            TradeName = (header.TradeName ?? string.Empty).Trim()
        };

    private static string FileName(string form, GstReturnPeriodRequest header, string extension)
    {
        var gstin = string.IsNullOrWhiteSpace(header.Gstin) ? "GSTIN" : header.Gstin.Trim().ToUpperInvariant();
        var period = string.IsNullOrWhiteSpace(header.ReturnPeriod) ? "MMYYYY" : header.ReturnPeriod.Trim();
        return $"Garmetix-{form}-{gstin}-{period}.{extension}";
    }

    private static readonly Gstr3BSuppliesSummary EmptySupplies = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Gstr3BInterStateSupply EmptyInterState = new(0, 0, 0, 0, 0, 0);
    private static readonly Gstr3BItcSummary EmptyItc = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Gstr3BInwardSummary EmptyInward = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Gstr3BInterestLateFee EmptyFee = new(0, 0, 0, 0, 0, 0);
}
