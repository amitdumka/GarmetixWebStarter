using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Purchase;

public static class VendorSettlementEndpoints
{
    public static RouteGroupBuilder MapVendorSettlementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/purchase")
            .WithTags("Vendor Settlements")
            .RequireAuthorization(GarmetixPolicies.Purchase);

        group.MapGet("/vendor-settlements/recent", ListAsync);
        group.MapGet("/vendor-settlements/{id:guid}", GetAsync);
        group.MapGet("/returns/{id:guid}/settlement-options", OptionsAsync);
        group.MapPost("/returns/{id:guid}/settle", SettleAsync);

        return group;
    }

    private static async Task<IReadOnlyList<VendorSettlementRowDto>> ListAsync(
        HttpContext context,
        GarmetixDbContext db,
        int take = 150,
        CancellationToken cancellationToken = default)
    {
        return await WorkspaceScope.ApplyTo(db.VendorSettlements.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(take, 1, 300))
            .Select(item => new VendorSettlementRowDto(
                item.Id,
                item.SettlementNumber,
                item.OnDate,
                item.VendorId,
                item.VendorName,
                item.PurchaseReturnId,
                item.ReturnNumber,
                item.DebitNoteId,
                item.DebitNoteNumber,
                item.SettlementType,
                item.AdjustedAmount,
                item.RefundAmount,
                item.TotalAmount,
                item.PaymentMode.HasValue ? item.PaymentMode.Value.ToString() : null,
                item.BankAccountId,
                item.ReferenceNumber,
                item.VoucherId,
                item.JournalEntryId,
                item.BankTransactionId,
                item.Status,
                item.Remarks))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var settlement = await WorkspaceScope.ApplyTo(db.VendorSettlements.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (settlement is null)
        {
            return Results.NotFound(new { message = "Vendor settlement was not found." });
        }

        var allocations = await db.VendorSettlementAllocations.AsNoTracking()
            .Where(item => item.VendorSettlementId == settlement.Id)
            .OrderBy(item => item.PurchaseInvoiceNumber)
            .Select(item => new VendorSettlementAllocationDto(
                item.Id,
                item.PurchaseInvoiceId,
                item.PurchaseInvoiceNumber,
                item.Amount))
            .ToListAsync(cancellationToken);
        var bankAccountName = settlement.BankAccountId.HasValue
            ? await db.BankAccounts.AsNoTracking()
                .Where(item => item.Id == settlement.BankAccountId.Value)
                .Select(item => item.AccountHolderName + " - " + item.AccountNumber)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var voucherNumber = settlement.VoucherId.HasValue
            ? await db.Vouchers.AsNoTracking()
                .Where(item => item.Id == settlement.VoucherId.Value)
                .Select(item => item.VoucherNumber)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var journalNumber = settlement.JournalEntryId.HasValue
            ? await db.JournalEntries.AsNoTracking()
                .Where(item => item.Id == settlement.JournalEntryId.Value)
                .Select(item => item.EntryNumber)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return Results.Ok(new VendorSettlementDetailDto(
            settlement.Id,
            settlement.SettlementNumber,
            settlement.OnDate,
            settlement.VendorId,
            settlement.VendorName,
            settlement.PurchaseReturnId,
            settlement.ReturnNumber,
            settlement.DebitNoteId,
            settlement.DebitNoteNumber,
            settlement.SettlementType,
            settlement.AdjustedAmount,
            settlement.RefundAmount,
            settlement.TotalAmount,
            settlement.PaymentMode?.ToString(),
            settlement.BankAccountId,
            bankAccountName,
            settlement.ReferenceNumber,
            settlement.VoucherId,
            voucherNumber,
            settlement.JournalEntryId,
            journalNumber,
            settlement.BankTransactionId,
            settlement.Status,
            settlement.Remarks,
            allocations));
    }

    private static async Task<IResult> OptionsAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var purchaseReturn = await WorkspaceScope.ApplyTo(db.PurchaseReturns.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (purchaseReturn is null)
        {
            return Results.NotFound(new { message = "Purchase return was not found." });
        }

        if (!purchaseReturn.DebitNoteId.HasValue)
        {
            return Results.BadRequest(new { message = "This purchase return has no linked debit note to settle." });
        }

        var note = await db.CommercialNotes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == purchaseReturn.DebitNoteId.Value, cancellationToken);
        if (note is null)
        {
            return Results.BadRequest(new { message = "The linked debit note was not found." });
        }

        var invoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => item.VendorId == purchaseReturn.VendorId
                && item.InvoiceStatus != InvoiceStatus.Cancelled
                && item.InvoiceStatus != InvoiceStatus.Refunded)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.OnDate)
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(item => item.Id).ToArray();
        var paidLookup = await db.PurchasePayments.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.PurchaseInvoiceId))
            .GroupBy(item => item.PurchaseInvoiceId)
            .Select(group => new { PurchaseInvoiceId = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToDictionaryAsync(item => item.PurchaseInvoiceId, item => item.Amount, cancellationToken);

        var outstanding = invoices
            .Select(invoice =>
            {
                paidLookup.TryGetValue(invoice.Id, out var paidAmount);
                var balance = Money(Math.Max(invoice.BillAmount - paidAmount, 0));
                return new VendorOutstandingInvoiceDto(
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.OnDate,
                    invoice.DueDate,
                    invoice.BillAmount,
                    Money(paidAmount),
                    balance,
                    invoice.InvoiceStatus.ToString());
            })
            .Where(item => item.OutstandingAmount > 0)
            .ToList();
        var availableAmount = Money(Math.Max(note.Amount - note.AdjustedAmount, 0));

        return Results.Ok(new VendorSettlementOptionDto(
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber,
            note.Id,
            note.NoteNumber,
            purchaseReturn.VendorId,
            purchaseReturn.VendorName,
            purchaseReturn.ReturnAmount,
            Money(note.AdjustedAmount),
            availableAmount,
            SettlementStatus(note.Amount, note.AdjustedAmount),
            outstanding));
    }

    private static async Task<IResult> SettleAsync(
        Guid id,
        VendorSettlementRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(140003, hashtext({"vendor-settlement|" + id.ToString("N")}));",
            cancellationToken);

        var purchaseReturn = await WorkspaceScope.ApplyTo(db.PurchaseReturns, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (purchaseReturn is null)
        {
            return Results.NotFound(new { message = "Purchase return was not found." });
        }

        if (!purchaseReturn.DebitNoteId.HasValue)
        {
            return Results.BadRequest(new { message = "This purchase return has no linked debit note to settle." });
        }

        var note = await db.CommercialNotes
            .FirstOrDefaultAsync(item => item.Id == purchaseReturn.DebitNoteId.Value, cancellationToken);
        var vendor = await db.Vendors
            .FirstOrDefaultAsync(item => item.Id == purchaseReturn.VendorId, cancellationToken);
        if (note is null || vendor is null)
        {
            return Results.BadRequest(new { message = "The linked vendor or debit note was not found." });
        }

        var requestedAllocations = request.Allocations ?? [];
        if (requestedAllocations.GroupBy(item => item.PurchaseInvoiceId).Any(group => group.Count() > 1))
        {
            return Results.BadRequest(new { message = "A purchase invoice can be selected only once." });
        }

        var requestedIds = requestedAllocations
            .Where(item => item.PurchaseInvoiceId != Guid.Empty)
            .Select(item => item.PurchaseInvoiceId)
            .Distinct()
            .ToArray();
        var invoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
            .Where(item => requestedIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        if (invoices.Count != requestedIds.Length || invoices.Any(item => item.VendorId != vendor.Id || item.InvoiceStatus is InvoiceStatus.Cancelled or InvoiceStatus.Refunded))
        {
            return Results.BadRequest(new { message = "One or more selected purchase invoices are not valid outstanding invoices for this vendor." });
        }

        var paidLookup = requestedIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await db.PurchasePayments.AsNoTracking()
                .Where(item => requestedIds.Contains(item.PurchaseInvoiceId))
                .GroupBy(item => item.PurchaseInvoiceId)
                .Select(group => new { PurchaseInvoiceId = group.Key, Amount = group.Sum(item => item.Amount) })
                .ToDictionaryAsync(item => item.PurchaseInvoiceId, item => item.Amount, cancellationToken);
        var requestedLookup = requestedAllocations
            .GroupBy(item => item.PurchaseInvoiceId)
            .ToDictionary(group => group.Key, group => Money(group.Sum(item => item.Amount)));
        var calculatorRows = invoices.Select(invoice =>
        {
            paidLookup.TryGetValue(invoice.Id, out var paid);
            requestedLookup.TryGetValue(invoice.Id, out var amount);
            return new VendorSettlementAllocationInput(invoice.Id, amount, Money(Math.Max(invoice.BillAmount - paid, 0)));
        }).ToList();

        VendorSettlementPlan plan;
        try
        {
            plan = VendorSettlementCalculator.Build(note.Amount, note.AdjustedAmount, request.RefundAmount, calculatorRows);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }

        if (plan.RefundAmount > 0)
        {
            if (!request.PaymentMode.HasValue || !IsRefundPaymentMode(request.PaymentMode.Value))
            {
                return Results.BadRequest(new { message = "Select Cash, Card, UPI, IMPS, RTGS, NEFT, Cheque, Demand Draft, Wallet, or Other for the refund receipt." });
            }

            if (request.PaymentMode.Value != PaymentMode.Cash && (!request.BankAccountId.HasValue || request.BankAccountId.Value == Guid.Empty))
            {
                return Results.BadRequest(new { message = "Select a bank account for a non-cash vendor refund." });
            }
        }

        var onDate = (request.OnDate ?? DateTime.Now).Date;
        var settlement = new VendorSettlement
        {
            SettlementNumber = await documentNumbers.NextVendorSettlementAsync(
                purchaseReturn.CompanyId,
                purchaseReturn.StoreGroupId,
                purchaseReturn.StoreId,
                onDate,
                cancellationToken),
            OnDate = onDate,
            VendorId = vendor.Id,
            VendorName = vendor.Name,
            PurchaseReturnId = purchaseReturn.Id,
            ReturnNumber = purchaseReturn.ReturnNumber,
            DebitNoteId = note.Id,
            DebitNoteNumber = note.NoteNumber,
            SettlementType = plan.SettlementType,
            AdjustedAmount = plan.AdjustedAmount,
            RefundAmount = plan.RefundAmount,
            TotalAmount = plan.TotalAmount,
            PaymentMode = plan.RefundAmount > 0 ? request.PaymentMode : null,
            BankAccountId = plan.RefundAmount > 0 && request.PaymentMode != PaymentMode.Cash ? request.BankAccountId : null,
            ReferenceNumber = Clean(request.ReferenceNumber),
            Status = "Posted",
            Remarks = Clean(request.Remarks),
            CompanyId = purchaseReturn.CompanyId,
            StoreGroupId = purchaseReturn.StoreGroupId,
            StoreId = purchaseReturn.StoreId
        };
        db.VendorSettlements.Add(settlement);

        foreach (var invoice in invoices)
        {
            requestedLookup.TryGetValue(invoice.Id, out var amount);
            if (amount <= 0)
            {
                continue;
            }

            paidLookup.TryGetValue(invoice.Id, out var alreadyPaid);
            db.VendorSettlementAllocations.Add(new VendorSettlementAllocation
            {
                VendorSettlementId = settlement.Id,
                PurchaseInvoiceId = invoice.Id,
                PurchaseInvoiceNumber = invoice.InvoiceNumber,
                Amount = amount,
                CompanyId = settlement.CompanyId
            });
            db.PurchasePayments.Add(new PurchasePayment
            {
                PurchaseInvoiceId = invoice.Id,
                VendorId = vendor.Id,
                OnDate = onDate,
                Amount = amount,
                PaymentMode = PaymentMode.DebitNote,
                ReferenceNumber = settlement.SettlementNumber,
                AdjustmentSourceType = "VendorSettlement",
                AdjustmentSourceId = settlement.Id,
                Remarks = $"Debit note {note.NoteNumber} adjusted through {settlement.SettlementNumber}.",
                CompanyId = settlement.CompanyId,
                StoreGroupId = settlement.StoreGroupId,
                StoreId = settlement.StoreId
            });

            var newPaid = Money(alreadyPaid + amount);
            if (invoice.InvoiceStatus is not InvoiceStatus.PartiallyRefunded)
            {
                invoice.InvoiceStatus = newPaid >= invoice.BillAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            }
        }

        Voucher? voucher = null;
        if (plan.RefundAmount > 0)
        {
            voucher = new Voucher
            {
                VoucherNumber = await documentNumbers.NextVendorRefundVoucherAsync(
                    settlement.CompanyId,
                    settlement.StoreGroupId,
                    settlement.StoreId,
                    onDate,
                    cancellationToken),
                OnDate = onDate,
                VoucherType = VoucherType.Receipt,
                PartyName = vendor.Name,
                Particulars = $"Vendor refund against debit note {note.NoteNumber} / return {purchaseReturn.ReturnNumber}",
                Amount = plan.RefundAmount,
                Remarks = settlement.Remarks ?? $"Vendor settlement {settlement.SettlementNumber}",
                SlipNumber = settlement.ReferenceNumber,
                PaymentMode = request.PaymentMode!.Value,
                PaymentDetails = settlement.ReferenceNumber,
                AccountNumber = settlement.BankAccountId,
                IsParty = true,
                CompanyId = settlement.CompanyId,
                StoreGroupId = settlement.StoreGroupId,
                StoreId = settlement.StoreId
            };
            db.Vouchers.Add(voucher);
            settlement.VoucherId = voucher.Id;

            var posting = await accounting.PostVendorRefundSettlementAsync(settlement, voucher, vendor, cancellationToken);
            settlement.JournalEntryId = posting.JournalEntryId;
            settlement.BankTransactionId = posting.BankTransactionId;
            vendor.Paid = Math.Max(vendor.Paid - plan.RefundAmount, 0);
        }

        note.AdjustedAmount = Money(note.AdjustedAmount + plan.TotalAmount);
        note.IsAdjusted = note.AdjustedAmount >= Money(note.Amount);
        purchaseReturn.SettledAmount = Money(note.AdjustedAmount);
        purchaseReturn.SettlementStatus = SettlementStatus(note.Amount, note.AdjustedAmount);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new VendorSettlementResponse(
            settlement.Id,
            settlement.SettlementNumber,
            settlement.SettlementType,
            settlement.AdjustedAmount,
            settlement.RefundAmount,
            settlement.TotalAmount,
            Money(Math.Max(note.Amount - note.AdjustedAmount, 0)),
            purchaseReturn.SettlementStatus,
            voucher?.Id,
            voucher?.VoucherNumber));
    }

    private static bool IsRefundPaymentMode(PaymentMode mode)
        => mode is PaymentMode.Cash
            or PaymentMode.Card
            or PaymentMode.UPI
            or PaymentMode.Wallets
            or PaymentMode.IMPS
            or PaymentMode.RTGS
            or PaymentMode.NEFT
            or PaymentMode.Cheque
            or PaymentMode.DemandDraft
            or PaymentMode.Others;

    private static string SettlementStatus(decimal total, decimal settled)
        => settled <= 0 ? "Open" : settled >= Money(total) ? "Settled" : "Partially Settled";

    private static decimal Money(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
