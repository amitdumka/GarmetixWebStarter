using System.Security.Cryptography;
using System.Text;
using Garmetix.Api.Inventory;
using Garmetix.Api.Numbering;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.SaleImport;

public sealed record VyaparCreditNoteImportItemRequest(
    string ItemName,
    string? Barcode,
    string? HsnCode,
    string? Category,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal Amount);

public sealed record VyaparCreditNoteImportRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string ReferenceNumber,
    DateTime OnDate,
    string PartyName,
    string? Reason,
    decimal Amount,
    IReadOnlyList<VyaparCreditNoteImportItemRequest> Items,
    Guid? MatchedInvoiceId = null,
    string? MatchNote = null);

public sealed record VyaparCreditNoteImportResponse(
    Guid CommercialNoteId,
    string NoteNumber,
    string ReferenceNumber,
    Guid CustomerId,
    string CustomerName,
    int ItemCount,
    int StockAdjustedCount,
    int StockUnmatchedCount,
    IReadOnlyList<string> Warnings);

/// <summary>
/// One-time backfill of historical Vyapar Sale Return / Credit Note transactions that predate
/// this app's own Sale Return feature. Deliberately does NOT reuse the live sale-return endpoint
/// (CreateReturnCoreAsync), which requires an existing InvoiceItem row and always posts a fresh
/// accounting journal - neither assumption holds for these historical records. Stock is always
/// reversed (the returned item is physically back in inventory); accounting posting is the
/// caller's decision per note (see MatchedInvoiceId/IsAdjusted below).
/// </summary>
public sealed class VyaparCreditNoteImportService(GarmetixDbContext db, StockLedgerService stockLedger)
{
    public async Task<VyaparCreditNoteImportResponse> ImportAsync(VyaparCreditNoteImportRequest request, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await ImportCoreAsync(request, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task<VyaparCreditNoteImportResponse> ImportCoreAsync(VyaparCreditNoteImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ReferenceNumber))
        {
            throw new InvalidOperationException("Reference number is required.");
        }

        var existing = await db.CommercialNotes.FirstOrDefaultAsync(
            note => note.CompanyId == request.CompanyId
                && note.SourceType == "VyaparCreditNoteImport"
                && note.SourceNumber == request.ReferenceNumber
                && !note.Deleted,
            cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Credit note {request.ReferenceNumber} was already imported as {existing.NoteNumber}.");
        }

        var customer = await GetOrCreateCustomerAsync(request.CompanyId, request.PartyName, cancellationToken);

        var noteId = Guid.NewGuid();
        var noteNumber = await DocumentNumberGenerator.NextAsync(db, request.CompanyId, null, request.StoreId, "CreditNote", "CN", request.OnDate, cancellationToken);

        var warnings = new List<string>();
        var itemEntities = new List<CommercialNoteItem>();
        var adjustedCount = 0;
        var unmatchedCount = 0;
        decimal taxableTotal = 0m;
        decimal taxTotal = 0m;

        foreach (var item in request.Items)
        {
            if (item.Amount <= 0 && item.Quantity <= 0)
            {
                continue;
            }

            var barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode.Trim();
            Product? product = null;
            Stock? stock = null;

            if (barcode is not null)
            {
                product = await db.Products.FirstOrDefaultAsync(p => p.CompanyId == request.CompanyId && p.Barcode == barcode, cancellationToken);
                if (product is not null)
                {
                    stock = await db.Stocks.FirstOrDefaultAsync(
                        s => s.CompanyId == request.CompanyId && s.StoreId == request.StoreId && s.Barcode == barcode && !s.IsOFB,
                        cancellationToken);
                }
            }

            var stockAdjusted = false;
            string? stockNote = null;

            if (stock is not null && item.Quantity > 0)
            {
                var snapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
                await stockLedger.PostAsync(stock, new StockMovement
                {
                    Barcode = stock.Barcode,
                    MovementType = "SalesReturnIn",
                    QuantityIn = item.Quantity,
                    CostPrice = snapshot.AverageCost,
                    MRP = item.UnitPrice,
                    TaxRate = item.TaxPercentage,
                    HSNCode = item.HsnCode ?? stock.HSNCode,
                    SourceType = "VyaparCreditNoteImport",
                    SourceId = noteId,
                    SourceNumber = request.ReferenceNumber,
                    Remarks = $"Historical credit note {request.ReferenceNumber} import - stock reversed",
                    OnDate = request.OnDate,
                    CompanyId = request.CompanyId,
                    StoreGroupId = request.StoreGroupId,
                    StoreId = request.StoreId
                }, cancellationToken);
                stockAdjusted = true;
                adjustedCount++;
            }
            else
            {
                stockNote = barcode is null
                    ? "No barcode on source row - stock not adjusted"
                    : product is null
                        ? $"Barcode {barcode} not found in Product master - stock not adjusted"
                        : "No stock row for this store/barcode - stock not adjusted";
                unmatchedCount++;
                warnings.Add($"{item.ItemName}: {stockNote}");
            }

            itemEntities.Add(new CommercialNoteItem
            {
                CommercialNoteId = noteId,
                ProductId = product?.Id,
                Barcode = barcode,
                ProductName = item.ItemName,
                HSNCode = item.HsnCode,
                Category = item.Category,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = item.DiscountAmount,
                TaxPercentage = item.TaxPercentage,
                TaxAmount = item.TaxAmount,
                Amount = item.Amount,
                StockAdjusted = stockAdjusted,
                StockAdjustmentNote = stockNote,
                CompanyId = request.CompanyId
            });

            taxTotal += item.TaxAmount;
            taxableTotal += item.Amount - item.TaxAmount;
        }

        var note = new CommercialNote
        {
            Id = noteId,
            NoteNumber = noteNumber,
            NoteType = NoteType.CreditNote,
            OnDate = request.OnDate,
            PartyType = PartyType.Customer,
            CustomerId = customer.Id,
            PartyName = customer.Name,
            PartyGstin = customer.GSTIN,
            SourceType = "VyaparCreditNoteImport",
            SourceId = request.MatchedInvoiceId,
            SourceNumber = request.ReferenceNumber,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Vyapar historical credit note import" : request.Reason.Trim(),
            TaxableAmount = Math.Round(Math.Max(taxableTotal, 0), 2),
            TaxAmount = Math.Round(Math.Max(taxTotal, 0), 2),
            Amount = request.Amount,
            IsAdjusted = request.MatchedInvoiceId.HasValue,
            AdjustedAmount = request.MatchedInvoiceId.HasValue ? request.Amount : 0,
            Remarks = request.MatchNote,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId
        };

        db.CommercialNotes.Add(note);
        db.CommercialNoteItems.AddRange(itemEntities);
        await db.SaveChangesAsync(cancellationToken);

        return new VyaparCreditNoteImportResponse(
            note.Id,
            note.NoteNumber,
            request.ReferenceNumber,
            customer.Id,
            customer.Name,
            itemEntities.Count,
            adjustedCount,
            unmatchedCount,
            warnings);
    }

    private async Task<Customer> GetOrCreateCustomerAsync(Guid companyId, string? partyNameRaw, CancellationToken cancellationToken)
    {
        var name = NormalizePartyName(partyNameRaw);
        var mobile = SyntheticMobileForName(name);

        var customer = await db.Customers.FirstOrDefaultAsync(
            item => item.CompanyId == companyId && item.MobileNumber == mobile,
            cancellationToken);
        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer
        {
            Name = name,
            MobileNumber = mobile,
            Address = "Dumka",
            City = "Dumka",
            State = "Jharkhand",
            Country = "India",
            CompanyId = companyId
        };
        db.Customers.Add(customer);
        return customer;
    }

    private static readonly HashSet<string> GenericPartyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cash", "Cash Customer", "Cash Sale", "Walk in", "Walk-in", "Walkin", ""
    };

    private static string NormalizePartyName(string? raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        return GenericPartyNames.Contains(trimmed) ? "Cash Sale" : trimmed;
    }

    private static string SyntheticMobileForName(string name)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToUpperInvariant()));
        var number = BitConverter.ToUInt32(hashBytes, 0) % 100000000;
        return $"90{number:D8}";
    }
}
