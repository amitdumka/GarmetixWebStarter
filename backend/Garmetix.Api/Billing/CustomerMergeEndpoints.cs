using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Garmetix.Api.Billing;

/// <summary>
/// One-off data correction: fold a customer record that should never have been a named customer (for example a
/// "no mobile" sale saved under the legacy WALKIN placeholder) into the standard Walk-in Customer (mobile 0000000000).
///
/// Every row that points at the source customer, its party or its party ledger - found from the EF model's
/// foreign keys plus any CustomerId / PartyId / LedgerId column - is re-pointed to the Walk-in customer, party and
/// ledger. The customer's name is preserved on each moved invoice (CustomerName + a Remarks line), then the source
/// customer, party and ledger are deleted. All of it runs in one transaction, and a database foreign-key violation
/// rolls everything back. The default call is a read-only preview.
/// </summary>
public static class CustomerMergeEndpoints
{
    public const string ConfirmationPhrase = "MERGE CUSTOMER";

    public static RouteGroupBuilder MapCustomerMergeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customer merge")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapPost("/{sourceId:guid}/merge-into-walkin/{targetId:guid}",
            (Guid sourceId, Guid targetId, MergeCustomerRequest? request, HttpContext context, GarmetixDbContext db, AccountingPostingService accounting, CancellationToken ct)
                => MergeAsync(sourceId, targetId, request, context, db, accounting, duplicateMode: false, ct));
        // Two customer records that share the same mobile number (a duplicate): fold the source into the target, keeping
        // every invoice's own name and mobile untouched.
        group.MapPost("/{sourceId:guid}/merge-duplicate/{targetId:guid}",
            (Guid sourceId, Guid targetId, MergeCustomerRequest? request, HttpContext context, GarmetixDbContext db, AccountingPostingService accounting, CancellationToken ct)
                => MergeAsync(sourceId, targetId, request, context, db, accounting, duplicateMode: true, ct));
        return group;
    }

    public sealed record MergeCustomerRequest(bool Confirm, string? ConfirmationText);

    private sealed record RefColumn(string Schema, string Table, string Column)
    {
        public string Quoted => $"\"{Schema}\".\"{Table}\"";
    }

    private static async Task<IResult> MergeAsync(
        Guid sourceId,
        Guid targetId,
        MergeCustomerRequest? request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        bool duplicateMode,
        CancellationToken cancellationToken)
    {
        request ??= new MergeCustomerRequest(false, null);

        var source = await db.Customers.FirstOrDefaultAsync(item => item.Id == sourceId, cancellationToken);
        var target = await db.Customers.FirstOrDefaultAsync(item => item.Id == targetId, cancellationToken);
        if (source is null || target is null)
        {
            return Results.NotFound(new { message = "Source or target customer was not found." });
        }

        if (source.Id == target.Id)
        {
            return Results.BadRequest(new { message = "Source and target must be different customers." });
        }

        if (source.CompanyId != target.CompanyId)
        {
            return Results.BadRequest(new { message = "Both customers must belong to the same company." });
        }

        if (!WorkspaceScope.HasFullAccess(context))
        {
            var companyClaim = WorkspaceScope.ClaimGuid(context, "companyId");
            if (companyClaim.HasValue && companyClaim.Value != source.CompanyId)
            {
                return Results.Forbid();
            }
        }

        if (duplicateMode)
        {
            var sourceMobile = (source.MobileNumber ?? string.Empty).Trim();
            if (BillingEndpoints.IsBlankCustomerMobile(sourceMobile) || sourceMobile == BillingEndpoints.WalkInMobileNumber
                || sourceMobile != (target.MobileNumber ?? string.Empty).Trim())
            {
                return Results.BadRequest(new { message = "Duplicate merge needs two customers with the same real mobile number." });
            }
        }
        else
        {
            if (target.MobileNumber != BillingEndpoints.WalkInMobileNumber)
            {
                return Results.BadRequest(new { message = $"The target must be the standard Walk-in Customer (mobile {BillingEndpoints.WalkInMobileNumber})." });
            }

            if (source.MobileNumber == BillingEndpoints.WalkInMobileNumber)
            {
                return Results.BadRequest(new { message = "The source is already a Walk-in Customer." });
            }
        }

        // Source party/ledger (may legitimately be missing for a customer that never had accounting posted).
        var sourceParty = source.PartyId.HasValue
            ? await db.Parties.FirstOrDefaultAsync(item => item.Id == source.PartyId.Value, cancellationToken)
            : null;
        var sourceLedgerId = sourceParty?.LedgerId;
        Ledger? sourceLedger = sourceLedgerId.HasValue && sourceLedgerId.Value != Guid.Empty
            ? await db.Ledgers.FirstOrDefaultAsync(item => item.Id == sourceLedgerId.Value, cancellationToken)
            : null;

        if (sourceParty is not null)
        {
            var sharedParty = await db.Customers.AnyAsync(item => item.PartyId == sourceParty.Id && item.Id != source.Id, cancellationToken);
            if (sharedParty)
            {
                return Results.Conflict(new { message = "Another customer shares this customer's accounting party; merge aborted." });
            }
        }

        if (sourceLedger is not null && sourceLedger.OpeningBalance != 0)
        {
            return Results.Conflict(new { message = $"The customer ledger has an opening balance of {sourceLedger.OpeningBalance:0.00}; clear or transfer it first, merge aborted." });
        }

        var customerRefs = FindReferences(db, typeof(Customer), "CustomerId", "Customers");
        var partyRefs = sourceParty is null ? [] : FindReferences(db, typeof(Party), "PartyId", "Customers", "Parties");
        var ledgerRefs = sourceLedger is null ? [] : FindReferences(db, typeof(Ledger), "LedgerId", "Parties", "Ledgers");

        var customerCounts = await CountAsync(db, customerRefs, source.Id, cancellationToken);
        var partyCounts = sourceParty is null ? new Dictionary<string, int>() : await CountAsync(db, partyRefs, sourceParty.Id, cancellationToken);
        var ledgerCounts = sourceLedger is null ? new Dictionary<string, int>() : await CountAsync(db, ledgerRefs, sourceLedger.Id, cancellationToken);

        var targetParty = target.PartyId.HasValue
            ? await db.Parties.FirstOrDefaultAsync(item => item.Id == target.PartyId.Value, cancellationToken)
            : null;

        var summary = new
        {
            source = new { source.Id, source.Name, source.MobileNumber, partyId = sourceParty?.Id, ledgerId = sourceLedger?.Id, ledgerName = sourceLedger?.Name },
            target = new { target.Id, target.Name, target.MobileNumber, partyId = targetParty?.Id, ledgerId = targetParty?.LedgerId },
            rowsToMove = new { byCustomerId = customerCounts, byPartyId = partyCounts, byLedgerId = ledgerCounts },
            willDelete = new { customer = source.Name, party = sourceParty?.Name, ledger = sourceLedger?.Name }
        };

        if (!request.Confirm)
        {
            return Results.Ok(new { preview = true, message = $"Preview only. Send confirm=true and confirmationText=\"{ConfirmationPhrase}\" to apply.", summary });
        }

        if (!string.Equals(request.ConfirmationText?.Trim(), ConfirmationPhrase, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = $"Type {ConfirmationPhrase} to confirm this permanent merge." });
        }

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                // 1) Keep the customer's name visible on each moved invoice, and stop showing the WALKIN placeholder mobile.
                var invoices = duplicateMode
                    ? new List<Invoice>()
                    : await db.SalesInvoices.IgnoreQueryFilters()
                        .Where(item => item.CustomerId == source.Id)
                        .ToListAsync(cancellationToken);
                if (duplicateMode)
                {
                    target.Amount += source.Amount;
                    target.BillCount += source.BillCount;
                    target.CreditBalance += source.CreditBalance;
                    target.LoyaltyPoints += source.LoyaltyPoints;
                }

                foreach (var invoice in invoices)
                {
                    if (string.IsNullOrWhiteSpace(invoice.CustomerName))
                    {
                        invoice.CustomerName = source.Name;
                    }

                    invoice.CustomerMobileNumber = BillingEndpoints.WalkInMobileNumber;
                    var remark = $"Customer: {source.Name} (moved to Walk-in Customer)";
                    invoice.Remarks = string.IsNullOrWhiteSpace(invoice.Remarks) ? remark : $"{invoice.Remarks}\n{remark}";
                }

                await db.SaveChangesAsync(cancellationToken);

                // 2) Target party + ledger (created through the normal posting path if the Walk-in customer has none yet).
                var resolvedTargetParty = targetParty ?? await accounting.EnsureCustomerPartyAsync(target, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                // 3) Re-point everything.
                await RepointAsync(db, customerRefs, source.Id, target.Id, cancellationToken);
                if (sourceParty is not null)
                {
                    await RepointAsync(db, partyRefs, sourceParty.Id, resolvedTargetParty.Id, cancellationToken);
                }

                if (sourceLedger is not null && resolvedTargetParty.LedgerId != Guid.Empty)
                {
                    await RepointAsync(db, ledgerRefs, sourceLedger.Id, resolvedTargetParty.LedgerId, cancellationToken);
                }

                // 4) Permanent removal of the source customer, party and ledger. A remaining foreign key aborts and rolls back.
                await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Customers\" WHERE \"Id\" = {0}", [source.Id], cancellationToken);
                if (sourceParty is not null)
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Parties\" WHERE \"Id\" = {0}", [sourceParty.Id], cancellationToken);
                }

                if (sourceLedger is not null)
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Ledgers\" WHERE \"Id\" = {0}", [sourceLedger.Id], cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (Exception exception) when (exception is DbUpdateException or Npgsql.PostgresException or InvalidOperationException)
        {
            return Results.Conflict(new { message = "Merge was rolled back; nothing was changed. " + (exception.InnerException?.Message ?? exception.Message), summary });
        }

        return Results.Ok(new { preview = false, merged = true, message = $"{source.Name} was merged into {target.Name} and deleted.", summary });
    }

    private static List<RefColumn> FindReferences(GarmetixDbContext db, Type principal, string namedColumn, params string[] excludedTables)
    {
        var excluded = new HashSet<string>(excludedTables, StringComparer.OrdinalIgnoreCase);
        var found = new List<RefColumn>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(table) || entityType.GetViewName() is not null || excluded.Contains(table))
            {
                continue;
            }

            var schema = entityType.GetSchema() ?? "public";
            var store = StoreObjectIdentifier.Table(table, entityType.GetSchema());

            void Add(string? column)
            {
                if (string.IsNullOrWhiteSpace(column) || column.Contains('"') || table.Contains('"') || schema.Contains('"'))
                {
                    return;
                }

                if (seen.Add($"{schema}.{table}.{column}"))
                {
                    found.Add(new RefColumn(schema, table, column));
                }
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                if (foreignKey.PrincipalEntityType.ClrType == principal && foreignKey.Properties.Count == 1)
                {
                    Add(foreignKey.Properties[0].GetColumnName(store));
                }
            }

            foreach (var property in entityType.GetProperties())
            {
                if (property.Name == namedColumn && (property.ClrType == typeof(Guid) || property.ClrType == typeof(Guid?)))
                {
                    Add(property.GetColumnName(store));
                }
            }
        }

        return found;
    }

    private static async Task<Dictionary<string, int>> CountAsync(GarmetixDbContext db, List<RefColumn> refs, Guid id, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>();
        foreach (var reference in refs)
        {
            var sql = $"SELECT COUNT(*)::int AS \"Value\" FROM {reference.Quoted} WHERE \"{reference.Column}\" = {{0}}";
            var count = await db.Database.SqlQueryRaw<int>(sql, id).FirstOrDefaultAsync(cancellationToken);
            if (count > 0)
            {
                counts[$"{reference.Table}.{reference.Column}"] = count;
            }
        }

        return counts;
    }

    private static async Task RepointAsync(GarmetixDbContext db, List<RefColumn> refs, Guid fromId, Guid toId, CancellationToken cancellationToken)
    {
        foreach (var reference in refs)
        {
            var sql = $"UPDATE {reference.Quoted} SET \"{reference.Column}\" = {{0}} WHERE \"{reference.Column}\" = {{1}}";
            await db.Database.ExecuteSqlRawAsync(sql, [toId, fromId], cancellationToken);
        }
    }
}
