using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Garmetix.Api.Commercial;

public static class CommercialEndpoints
{
    public static RouteGroupBuilder MapCommercialEndpoints(this WebApplication app)
    {
        var notes = app.MapGroup("/api/commercial-notes")
            .WithTags("Debit Credit Notes")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        notes.MapGet("/", ListNotesAsync);
        notes.MapGet("/{id:guid}", GetNoteAsync);
        notes.MapPost("/", CreateNoteAsync);
        notes.MapPut("/{id:guid}", UpdateNoteAsync).RequireAuthorization(GarmetixPolicies.Edit);
        notes.MapGet("/{id:guid}/pdf", DownloadNotePdfAsync);
        notes.MapPost("/{id:guid}/mark-printed", MarkNotePrintedAsync);

        var advances = app.MapGroup("/api/customer-advances")
            .WithTags("Customer Advances")
            .RequireAuthorization(GarmetixPolicies.Billing);

        advances.MapGet("/", ListAdvanceReceiptsAsync);
        advances.MapPost("/receipts", CreateAdvanceReceiptAsync);

        var loyalty = app.MapGroup("/api/loyalty")
            .WithTags("Loyalty")
            .RequireAuthorization(GarmetixPolicies.Billing);

        loyalty.MapGet("/program", GetProgramAsync);
        loyalty.MapPost("/program", SaveProgramAsync).RequireAuthorization(GarmetixPolicies.Edit);
        loyalty.MapGet("/customers/{customerId:guid}", GetCustomerLoyaltySummaryAsync);
        loyalty.MapGet("/customers/{customerId:guid}/ledger", GetCustomerLoyaltyLedgerAsync);
        loyalty.MapPost("/customers/{customerId:guid}/adjust", AdjustCustomerLoyaltyAsync).RequireAuthorization(GarmetixPolicies.Edit);

        return notes;
    }

    private static async Task<IReadOnlyList<CommercialNoteRow>> ListNotesAsync(HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, int take = 100, NoteType? noteType = null, CancellationToken cancellationToken = default)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);

        var query = WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context);
        if (noteType.HasValue)
        {
            query = query.Where(item => item.NoteType == noteType.Value);
        }

        return await query
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(take, 1, 300))
            .Select(item => new CommercialNoteRow(
                item.Id,
                item.NoteNumber,
                item.OnDate,
                item.NoteType.ToString(),
                item.PartyType.ToString(),
                item.PartyName,
                item.PartyGstin,
                item.SourceType,
                item.SourceNumber,
                item.TaxableAmount,
                item.TaxAmount,
                item.Amount,
                item.IsAdjusted,
                item.AdjustedAmount,
                item.Reason))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> GetNoteAsync(Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);
        var note = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return note is null ? Results.NotFound() : Results.Ok(note);
    }

    private static async Task<IResult> CreateNoteAsync(CommercialNoteRequest request, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);

        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Note amount must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.PartyName))
        {
            return Results.BadRequest(new { message = "Party name is required." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Selected note store is outside your access scope." });
        }

        var note = new CommercialNote
        {
            NoteNumber = await CreateNoteNumberAsync(request.CompanyId, request.StoreId, request.NoteType, db, cancellationToken),
            NoteType = request.NoteType,
            OnDate = DateTime.Now,
            PartyType = request.PartyType,
            PartyId = request.PartyId,
            CustomerId = request.CustomerId,
            VendorId = request.VendorId,
            PartyName = request.PartyName.Trim(),
            PartyGstin = string.IsNullOrWhiteSpace(request.PartyGstin) ? null : request.PartyGstin.Trim().ToUpperInvariant(),
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Manual" : request.SourceType.Trim(),
            SourceId = request.SourceId,
            SourceNumber = request.SourceNumber,
            Reason = request.Reason?.Trim() ?? string.Empty,
            TaxableAmount = Math.Max(request.TaxableAmount, 0),
            TaxAmount = Math.Max(request.TaxAmount, 0),
            Amount = request.Amount,
            IsAdjusted = false,
            AdjustedAmount = 0,
            Remarks = request.Remarks,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId
        };

        db.CommercialNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/commercial-notes/{note.Id}", note);
    }

    private static async Task<IResult> UpdateNoteAsync(Guid id, CommercialNoteRequest request, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);

        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Note amount must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.PartyName))
        {
            return Results.BadRequest(new { message = "Party name is required." });
        }

        var note = await WorkspaceScope.ApplyTo(db.CommercialNotes, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (note is null)
        {
            return Results.NotFound();
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Selected note store is outside your access scope." });
        }

        if (!string.Equals(note.SourceType, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "Only manual debit/credit notes can be edited. Return-generated notes should remain linked to their source document." });
        }

        note.NoteType = request.NoteType;
        note.PartyType = request.PartyType;
        note.PartyId = request.PartyId;
        note.CustomerId = request.CustomerId;
        note.VendorId = request.VendorId;
        note.PartyName = request.PartyName.Trim();
        note.PartyGstin = string.IsNullOrWhiteSpace(request.PartyGstin) ? null : request.PartyGstin.Trim().ToUpperInvariant();
        note.Reason = request.Reason?.Trim() ?? string.Empty;
        note.TaxableAmount = Math.Max(request.TaxableAmount, 0);
        note.TaxAmount = Math.Max(request.TaxAmount, 0);
        note.Amount = request.Amount;
        note.Remarks = request.Remarks;
        note.CompanyId = request.CompanyId;
        note.StoreGroupId = request.StoreGroupId;
        note.StoreId = request.StoreId;
        note.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(note);
    }

    private static async Task<IResult> DownloadNotePdfAsync(Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, bool a5Slip = false, bool reprint = false, bool signatures = true, CancellationToken cancellationToken = default)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);
        var note = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (note is null)
        {
            return Results.NotFound();
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == note.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == note.StoreId, cancellationToken);
        var model = new VoucherPdfModel(
            company?.Name ?? "Garmetix",
            FormatAddress(company?.Address, company?.City, company?.State, company?.ZipCode),
            company?.ContactNumber ?? string.Empty,
            company?.GSTIN ?? string.Empty,
            store?.Name ?? "Store",
            note.NoteNumber,
            note.OnDate,
            note.NoteType == NoteType.CreditNote ? "Credit Note" : "Debit Note",
            note.PartyName,
            string.IsNullOrWhiteSpace(note.Reason) ? $"{note.NoteType} issued manually" : note.Reason,
            note.Amount,
            note.Remarks,
            note.SourceNumber,
            "Note",
            note.SourceType,
            string.Empty,
            string.Empty,
            string.Empty,
            Garmetix.Api.ProductLookup.DocumentCodeService.Create(Garmetix.Api.ProductLookup.DocumentCodeService.CommercialNote, note.Id));
        var pdf = VoucherPdfDocument.Build(model, a5Slip, reprint, signatures);
        var safeNumber = Regex.Replace(note.NoteNumber, @"[^A-Za-z0-9_-]+", "-").Trim('-');
        return Results.File(pdf, "application/pdf", $"{safeNumber}-{(a5Slip ? "a5-slip" : "a4-two-copy")}.pdf");
    }

    private static async Task<IResult> MarkNotePrintedAsync(Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);
        var note = await WorkspaceScope.ApplyTo(db.CommercialNotes, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (note is null)
        {
            return Results.NotFound();
        }

        note.Printed = true;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { note.Id, note.NoteNumber, note.Printed });
    }

    private static async Task<IReadOnlyList<CustomerAdvanceReceiptRow>> ListAdvanceReceiptsAsync(HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, int take = 100, CancellationToken cancellationToken = default)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);

        return await WorkspaceScope.ApplyTo(db.CustomerAdvanceReceipts.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(take, 1, 300))
            .Select(item => new CustomerAdvanceReceiptRow(
                item.Id,
                item.ReceiptNumber,
                item.OnDate,
                item.CustomerId,
                item.CustomerName,
                item.CustomerMobileNumber,
                item.Amount,
                item.AdjustedAmount,
                item.AvailableAmount,
                item.PaymentMode.ToString(),
                item.ReferenceNumber))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> CreateAdvanceReceiptAsync(CustomerAdvanceReceiptRequest request, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);

        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Advance receipt amount must be greater than zero." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Selected receipt store is outside your access scope." });
        }

        var mobile = string.IsNullOrWhiteSpace(request.CustomerMobileNumber) ? "ADVANCE" : request.CustomerMobileNumber.Trim();
        var name = string.IsNullOrWhiteSpace(request.CustomerName) ? "Advance Customer" : request.CustomerName.Trim();
        var customer = request.CustomerId.HasValue
            ? await db.Customers.FirstOrDefaultAsync(item => item.Id == request.CustomerId.Value, cancellationToken)
            : await db.Customers.FirstOrDefaultAsync(item => item.CompanyId == request.CompanyId && item.MobileNumber == mobile, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Name = name,
                MobileNumber = mobile,
                CompanyId = request.CompanyId
            };
            db.Customers.Add(customer);
        }

        customer.CreditBalance += request.Amount;
        var receipt = new CustomerAdvanceReceipt
        {
            ReceiptNumber = await CreateAdvanceReceiptNumberAsync(request.CompanyId, request.StoreId, db, cancellationToken),
            OnDate = DateTime.Now,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerMobileNumber = customer.MobileNumber,
            Amount = request.Amount,
            AdjustedAmount = 0,
            AvailableAmount = request.Amount,
            PaymentMode = request.PaymentMode,
            BankAccountId = request.BankAccountId,
            ReferenceNumber = request.ReferenceNumber,
            Remarks = request.Remarks,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId
        };

        db.CustomerAdvanceReceipts.Add(receipt);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/customer-advances/{receipt.Id}", receipt);
    }

    private static async Task<IResult> GetProgramAsync(HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, Guid? storeId, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);
        var query = WorkspaceScope.ApplyTo(db.LoyaltyPrograms.AsNoTracking(), context);
        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        var program = await query.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return program is null ? Results.Ok(null) : Results.Ok(ToDto(program));
    }

    private static async Task<IResult> SaveProgramAsync(LoyaltyProgramRequest request, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);
        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Selected loyalty store is outside your access scope." });
        }

        var program = await db.LoyaltyPrograms.FirstOrDefaultAsync(item => item.CompanyId == request.CompanyId && item.StoreId == request.StoreId, cancellationToken);
        if (program is null)
        {
            program = new LoyaltyProgram { CompanyId = request.CompanyId, StoreGroupId = request.StoreGroupId, StoreId = request.StoreId };
            db.LoyaltyPrograms.Add(program);
        }

        program.Enabled = request.Enabled;
        program.Name = string.IsNullOrWhiteSpace(request.Name) ? "Garmetix Loyalty" : request.Name.Trim();
        program.EarnPointsPerRupee = Math.Max(request.EarnPointsPerRupee, 0);
        program.RedeemValuePerPoint = Math.Max(request.RedeemValuePerPoint, 0);
        program.MinimumBillAmount = Math.Max(request.MinimumBillAmount, 0);
        program.ExpiryDays = request.ExpiryDays;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(program));
    }

    private static async Task<IReadOnlyList<LoyaltyLedgerRow>> GetCustomerLoyaltyLedgerAsync(Guid customerId, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);

        return await WorkspaceScope.ApplyTo(db.LoyaltyPointLedgers.AsNoTracking(), context)
            .Where(item => item.CustomerId == customerId)
            .OrderByDescending(item => item.OnDate)
            .Select(item => new LoyaltyLedgerRow(item.Id, item.OnDate, item.CustomerId, item.CustomerName, item.SourceType, item.SourceNumber, item.PointsIn, item.PointsOut, item.BalanceAfter, item.Remarks))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> GetCustomerLoyaltySummaryAsync(Guid customerId, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);

        var customer = await WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new CustomerLoyaltySummary(
            customer.Id,
            customer.Name,
            customer.LoyaltyPoints,
            customer.CreditBalance,
            customer.Amount,
            customer.BillCount));
    }

    private static async Task<IResult> AdjustCustomerLoyaltyAsync(Guid customerId, LoyaltyAdjustmentRequest request, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureCommercialSchemaAsync(db, loggerFactory, cancellationToken);

        if (request.PointsIn <= 0 && request.PointsOut <= 0)
        {
            return Results.BadRequest(new { message = "Enter points to add or redeem." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Selected loyalty store is outside your access scope." });
        }

        var customer = await WorkspaceScope.ApplyTo(db.Customers, context).FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return Results.NotFound();
        }

        var pointsIn = Math.Max(request.PointsIn, 0);
        var pointsOut = Math.Max(request.PointsOut, 0);
        if (pointsOut > customer.LoyaltyPoints + pointsIn)
        {
            return Results.BadRequest(new { message = "Redeem points cannot exceed the customer's available loyalty balance." });
        }

        customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints + pointsIn - pointsOut);
        db.LoyaltyPointLedgers.Add(new LoyaltyPointLedger
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            OnDate = DateTime.Now,
            SourceType = "ManualAdjustment",
            SourceNumber = pointsIn > 0 ? "LOY-ADD" : "LOY-REDEEM",
            PointsIn = pointsIn,
            PointsOut = pointsOut,
            BalanceAfter = customer.LoyaltyPoints,
            Remarks = request.Remarks,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new CustomerLoyaltySummary(customer.Id, customer.Name, customer.LoyaltyPoints, customer.CreditBalance, customer.Amount, customer.BillCount));
    }

    internal static async Task<CommercialNote> CreateCreditNoteFromSalesReturnAsync(Invoice returnInvoice, Customer customer, string? reason, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var storeGroupId = await db.Stores.AsNoTracking()
            .Where(item => item.Id == returnInvoice.StoreId)
            .Select(item => item.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);

        var note = new CommercialNote
        {
            NoteNumber = await CreateNoteNumberAsync(returnInvoice.CompanyId, returnInvoice.StoreId, NoteType.CreditNote, db, cancellationToken),
            NoteType = NoteType.CreditNote,
            OnDate = DateTime.Now,
            PartyType = PartyType.Customer,
            CustomerId = customer.Id,
            PartyName = customer.Name,
            PartyGstin = customer.GSTIN,
            SourceType = "SalesReturn",
            SourceId = returnInvoice.Id,
            SourceNumber = returnInvoice.InvoiceNumber,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Sales return credit note" : reason,
            TaxableAmount = returnInvoice.NetAmount,
            TaxAmount = returnInvoice.TaxAmount,
            Amount = returnInvoice.BillAmount,
            IsAdjusted = false,
            CompanyId = returnInvoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = returnInvoice.StoreId
        };
        db.CommercialNotes.Add(note);
        return note;
    }

    internal static Task<CommercialNote> CreateDebitNoteFromPurchaseReturnAsync(PurchaseInvoice purchaseInvoice, Vendor vendor, string? reason, Guid storeGroupId, Guid storeId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        return CreateDebitNoteFromPurchaseReturnAsync(
            purchaseInvoice,
            vendor,
            reason,
            storeGroupId,
            storeId,
            purchaseInvoice.BasePrice,
            purchaseInvoice.TaxAmount,
            purchaseInvoice.BillAmount,
            null,
            db,
            cancellationToken);
    }

    internal static async Task<CommercialNote> CreateDebitNoteFromPurchaseReturnAsync(
        PurchaseInvoice purchaseInvoice,
        Vendor vendor,
        string? reason,
        Guid storeGroupId,
        Guid storeId,
        decimal taxableAmount,
        decimal taxAmount,
        decimal amount,
        string? itemSummary,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var note = new CommercialNote
        {
            NoteNumber = await CreateNoteNumberAsync(purchaseInvoice.CompanyId, storeId, NoteType.DebitNote, db, cancellationToken),
            NoteType = NoteType.DebitNote,
            OnDate = DateTime.Now,
            PartyType = PartyType.Vendor,
            VendorId = vendor.Id,
            PartyName = vendor.Name,
            PartyGstin = vendor.GSTIN,
            SourceType = "PurchaseReturn",
            SourceId = purchaseInvoice.Id,
            SourceNumber = purchaseInvoice.InvoiceNumber,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Purchase return debit note" : reason,
            TaxableAmount = Math.Round(Math.Max(taxableAmount, 0), 2),
            TaxAmount = Math.Round(Math.Max(taxAmount, 0), 2),
            Amount = Math.Round(Math.Max(amount, 0), 2),
            IsAdjusted = false,
            Remarks = string.IsNullOrWhiteSpace(itemSummary) ? null : itemSummary,
            CompanyId = purchaseInvoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        };
        db.CommercialNotes.Add(note);
        return note;
    }

    private static Task EnsureCommercialSchemaAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        return DatabaseSchemaRepairService.RepairKnownSchemaDriftAsync(db, loggerFactory.CreateLogger("DatabaseSchemaRepair"), cancellationToken);
    }

    private static LoyaltyProgramDto ToDto(LoyaltyProgram program) => new(program.Id, program.CompanyId, program.StoreGroupId, program.StoreId, program.Enabled, program.Name, program.EarnPointsPerRupee, program.RedeemValuePerPoint, program.MinimumBillAmount, program.ExpiryDays);

    private static Task<string> CreateNoteNumberAsync(Guid companyId, Guid storeId, NoteType noteType, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var prefix = noteType == NoteType.CreditNote ? "CN" : "DN";
        var documentType = noteType == NoteType.CreditNote ? "CreditNote" : "DebitNote";
        return DocumentNumberGenerator.NextAsync(db, companyId, null, storeId, documentType, prefix, DateTime.Today, cancellationToken);
    }

    private static Task<string> CreateAdvanceReceiptNumberAsync(Guid companyId, Guid storeId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        return DocumentNumberGenerator.NextAsync(db, companyId, null, storeId, "CustomerAdvanceReceipt", "ADV", DateTime.Today, cancellationToken);
    }

    private static string FormatAddress(params string?[] parts)
    {
        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
    }
}
