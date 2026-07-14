using System.Text.Json;
using Garmetix.Core.Models.Audit;

namespace Garmetix.Api.InvoiceReplacement;

internal static class InvoiceReplacementAudit
{
    public const string Module = "Invoice Replacement";

    public static void Add(
        Garmetix.Infrastructure.Data.GarmetixDbContext db,
        HttpContext context,
        string action,
        string entityName,
        Guid entityId,
        string entityDisplayName,
        string reference,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string reason,
        object? before = null,
        object? after = null)
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Action = action,
            Module = Module,
            EntityName = entityName,
            EntityDisplayName = entityDisplayName,
            EntityId = entityId,
            Reference = reference,
            CompanyId = companyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId,
            UserId = ReadGuidClaim(context, System.Security.Claims.ClaimTypes.NameIdentifier),
            UserName = FirstNonBlank(context.User.Identity?.Name, context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value, context.User.FindFirst("name")?.Value, "System"),
            Source = "InvoiceReplacementApproval",
            RequestMethod = context.Request.Method,
            RequestPath = context.Request.Path.Value,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            TraceIdentifier = context.TraceIdentifier,
            Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            ChangesJson = JsonSerializer.Serialize(new[] { new { field = "ReplacementApproval", before = before?.GetType().Name, after = after?.GetType().Name } }),
            ChangedFieldCount = before is null && after is null ? 0 : 1
        });
    }

    private static Guid? ReadGuidClaim(HttpContext context, string claimType)
        => Guid.TryParse(context.User.FindFirst(claimType)?.Value, out var value) ? value : null;

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "System";
}
