using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.GstTax;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>GST audit rule catalog + findings review + the run trigger (spec section 5.7/11).</summary>
public static class GstAuditEndpoints
{
    public static RouteGroupBuilder MapGstAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst/audit")
            .WithTags("GST & Taxes - Audit")
            .RequireAuthorization(GarmetixPolicies.Gst);

        group.MapGet("/rules", GetRulesAsync);
        group.MapPut("/rules/{id:guid}", UpdateRuleAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/run", RunAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/findings", GetFindingsAsync);
        group.MapPost("/findings/{id:guid}/ignore", (Guid id, HttpContext ctx, GarmetixDbContext db, ILoggerFactory lf, CancellationToken ct) => SetStatusAsync(id, "Ignored", ctx, db, lf, ct)).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/findings/{id:guid}/mark-fixed", (Guid id, HttpContext ctx, GarmetixDbContext db, ILoggerFactory lf, CancellationToken ct) => SetStatusAsync(id, "Fixed", ctx, db, lf, ct)).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/findings/{id:guid}/accept", (Guid id, HttpContext ctx, GarmetixDbContext db, ILoggerFactory lf, CancellationToken ct) => SetStatusAsync(id, "Accepted", ctx, db, lf, ct)).RequireAuthorization(GarmetixPolicies.Edit);

        return group;
    }

    private static async Task EnsureStorageAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairGstTaxStorageAsync(db, loggerFactory.CreateLogger("GstTaxStorageRepair"), cancellationToken);
    }

    private static async Task<IResult> GetRulesAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);
        var rules = await db.GstAuditRules.AsNoTracking().OrderBy(r => r.ModuleArea).ThenBy(r => r.RuleCode).ToListAsync(cancellationToken);
        return Results.Ok(rules.Select(r => new GstAuditRuleDto(r.Id, r.RuleCode, r.RuleName, r.ModuleArea, r.Severity, r.IsEnabled, r.StrictMode, r.MessageTemplate)));
    }

    private static async Task<IResult> UpdateRuleAsync(
        Guid id,
        GstAuditRuleUpdateRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var rule = await db.GstAuditRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
        {
            return Results.NotFound(new { message = "Audit rule not found." });
        }

        rule.IsEnabled = request.IsEnabled;
        rule.StrictMode = request.StrictMode;
        rule.MessageTemplate = request.MessageTemplate;
        rule.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new GstAuditRuleDto(rule.Id, rule.RuleCode, rule.RuleName, rule.ModuleArea, rule.Severity, rule.IsEnabled, rule.StrictMode, rule.MessageTemplate));
    }

    private static async Task<IResult> RunAsync(
        GstAuditRunRequest request,
        HttpContext context,
        GstAuditEngineService engine,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);
        var result = await engine.RunAsync(context, request.ModuleArea, request.FromDate, request.ToDate, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetFindingsAsync(
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        string? status = null,
        string? severity = null,
        string? moduleArea = null,
        Guid? invoiceId = null,
        Guid? purchaseInvoiceId = null,
        int limit = 200)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var query = WorkspaceScope.ApplyTo(db.GstAuditFindings.AsNoTracking().Where(f => !f.Deleted), context);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(f => f.Severity == severity);
        }
        if (!string.IsNullOrWhiteSpace(moduleArea))
        {
            query = query.Where(f => f.ModuleArea == moduleArea);
        }
        if (invoiceId.HasValue)
        {
            query = query.Where(f => f.InvoiceId == invoiceId.Value);
        }
        if (purchaseInvoiceId.HasValue)
        {
            query = query.Where(f => f.PurchaseInvoiceId == purchaseInvoiceId.Value);
        }

        var rows = await query
            .OrderByDescending(f => f.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(f => new GstAuditFindingDto(f.Id, f.RuleCode, f.Severity, f.ModuleArea, f.EntityType, f.EntityId, f.Gstin, f.HsnCode, f.Message, f.ExpectedValue, f.ActualValue, f.Status, f.CreatedAt, f.ReviewedBy, f.ReviewedAt, f.InvoiceId, f.PurchaseInvoiceId))
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> SetStatusAsync(
        Guid id,
        string status,
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var finding = await db.GstAuditFindings.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (finding is null)
        {
            return Results.NotFound(new { message = "Finding not found." });
        }

        finding.Status = status;
        finding.ReviewedBy = context.User.Identity?.Name;
        finding.ReviewedAt = DateTime.UtcNow;
        finding.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = $"Finding marked {status}." });
    }
}
