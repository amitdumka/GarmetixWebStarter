using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.ProductLookup;

public static class ProductLookupEndpoints
{
    public static RouteGroupBuilder MapProductLookupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/product-lookup")
            .WithTags("Product Lookup")
            .RequireAuthorization();

        group.MapGet("/", SearchProductsAsync);
        group.MapGet("/barcode/{barcode}", GetByBarcodeAsync);

        var scan = app.MapGroup("/api/scan")
            .WithTags("Scan Lookup")
            .RequireAuthorization();
        scan.MapGet("/{code}", ScanCodeAsync);
        scan.MapGet("/qr/{documentType}/{id:guid}.svg", (string documentType, Guid id) =>
            Results.Text(
                DocumentCodeService.BuildSvg(DocumentCodeService.Create(documentType, id)),
                "image/svg+xml",
                System.Text.Encoding.UTF8));

        return group;
    }

    private static async Task<IReadOnlyList<ProductLookupRow>> SearchProductsAsync(
        HttpContext context,
        GarmetixDbContext db,
        string? query,
        Guid? storeId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        var term = query?.Trim().ToLowerInvariant();
        var stocks = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(item => !item.IsOFB)
            .Include(item => item.Product)!
            .ThenInclude(product => product!.ProductCategory)
            .Include(item => item.Product)!
            .ThenInclude(product => product!.ProductSubCategory)
            .Include(item => item.Tax)
            .AsQueryable();

        if (storeId.HasValue)
        {
            stocks = stocks.Where(item => item.StoreId == storeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            stocks = stocks.Where(item => item.Barcode.ToLower().Contains(term) || (item.Product != null && item.Product.Name.ToLower().Contains(term)) || (item.HSNCode != null && item.HSNCode.ToLower().Contains(term)));
        }

        return await stocks
            .OrderBy(item => item.Product!.Name)
            .ThenBy(item => item.Barcode)
            .Take(take)
            .Select(item => new ProductLookupRow(
                item.ProductId,
                item.Id,
                item.Product != null ? item.Product.Name : item.Barcode,
                item.Barcode,
                item.HSNCode ?? item.Product!.HSNCode,
                item.PurchaseQty - item.SoldQty,
                item.MRP,
                item.TaxRate,
                item.TaxType.ToString(),
                item.Unit.ToString(),
                item.Product != null && item.Product.ProductCategory != null ? item.Product.ProductCategory.Name : string.Empty,
                item.Product != null && item.Product.ProductSubCategory != null ? item.Product.ProductSubCategory.Name : string.Empty,
                item.TaxId,
                item.Product != null ? item.Product.ProductCategoryId : null,
                item.Product != null ? item.Product.ProductSubCategoryId : null))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> GetByBarcodeAsync(
        string barcode,
        HttpContext context,
        GarmetixDbContext db,
        Guid? storeId,
        CancellationToken cancellationToken)
    {
        var rows = await SearchProductsAsync(context, db, barcode, storeId, 5, cancellationToken);
        var exact = rows.FirstOrDefault(item => string.Equals(item.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
        return exact is null ? Results.NotFound(new { message = "Product barcode was not found." }) : Results.Ok(exact);
    }

    private static async Task<IResult> ScanCodeAsync(string code, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant().Trim('*');
        if (DocumentCodeService.TryParse(normalized, out var documentType, out var documentId))
        {
            var tokenResult = await FindByTokenAsync(documentType, documentId, normalized, context, db, cancellationToken);
            return tokenResult is null
                ? Results.NotFound(new { message = "The scanned document is not available in your permitted workspace." })
                : Results.Ok(tokenResult);
        }

        var idPart = normalized.Contains('-') ? normalized[(normalized.LastIndexOf('-') + 1)..] : normalized;

        if (normalized.StartsWith("INV-") || normalized.StartsWith("S-"))
        {
            var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .OrderByDescending(item => item.OnDate)
                .FirstOrDefaultAsync(item => item.Id.ToString("N").StartsWith(idPart.ToLowerInvariant()) || item.InvoiceNumber.ToUpper() == normalized || item.InvoiceNumber.ToUpper().EndsWith(idPart), cancellationToken);
            if (invoice is not null)
            {
                return Results.Ok(new ScanLookupResult(normalized, "SaleInvoice", invoice.Id, invoice.InvoiceNumber, invoice.OnDate, invoice.CustomerName ?? "Walk-in Customer", invoice.BillAmount, $"/billing?invoiceId={invoice.Id}"));
            }
        }

        if (normalized.StartsWith("PINV-") || normalized.StartsWith("P-"))
        {
            var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
                .OrderByDescending(item => item.OnDate)
                .FirstOrDefaultAsync(item => item.Id.ToString("N").StartsWith(idPart.ToLowerInvariant()) || item.InvoiceNumber.ToUpper() == normalized || item.InvoiceNumber.ToUpper().EndsWith(idPart), cancellationToken);
            if (invoice is not null)
            {
                return Results.Ok(new ScanLookupResult(normalized, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, invoice.OnDate, invoice.VendorName ?? "Supplier", invoice.BillAmount, $"/purchase?invoiceId={invoice.Id}"));
            }
        }

        if (normalized.StartsWith("VCH-") || normalized.StartsWith("V-"))
        {
            var voucher = await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context)
                .OrderByDescending(item => item.OnDate)
                .FirstOrDefaultAsync(item => item.Id.ToString("N").StartsWith(idPart.ToLowerInvariant()) || item.VoucherNumber.ToUpper() == normalized || item.VoucherNumber.ToUpper().EndsWith(idPart), cancellationToken);
            if (voucher is not null)
            {
                return Results.Ok(new ScanLookupResult(normalized, "Voucher", voucher.Id, voucher.VoucherNumber, voucher.OnDate, voucher.PartyName, voucher.Amount, $"/vouchers?voucherId={voucher.Id}"));
            }
        }

        if (normalized.StartsWith("CV-") || normalized.StartsWith("CASH-"))
        {
            var voucher = await WorkspaceScope.ApplyTo(db.CashVouchers.AsNoTracking(), context)
                .OrderByDescending(item => item.OnDate)
                .FirstOrDefaultAsync(item => item.Id.ToString("N").StartsWith(idPart.ToLowerInvariant()) || item.VoucherNumber.ToUpper() == normalized || item.VoucherNumber.ToUpper().EndsWith(idPart), cancellationToken);
            if (voucher is not null)
            {
                return Results.Ok(new ScanLookupResult(normalized, "CashVoucher", voucher.Id, voucher.VoucherNumber, voucher.OnDate, voucher.PartyName, voucher.Amount, $"/cash-vouchers?voucherId={voucher.Id}"));
            }
        }

        if (normalized.Contains("/NGP/") || normalized.Contains("/NGS/") || normalized.StartsWith("NGP-") || normalized.StartsWith("NGS-"))
        {
            var document = await WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context)
                .OrderByDescending(item => item.OnDate)
                .FirstOrDefaultAsync(item => item.DocumentNumber.ToUpper() == normalized || item.DocumentNumber.ToUpper().EndsWith(idPart), cancellationToken);
            if (document is not null)
            {
                return Results.Ok(NonGstResult(normalized, document));
            }
        }

        var note = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .FirstOrDefaultAsync(item => item.NoteNumber.ToUpper() == normalized || item.NoteNumber.ToUpper().EndsWith(idPart), cancellationToken);
        if (note is not null)
        {
            return Results.Ok(NoteResult(normalized, note));
        }

        if (normalized.Contains("/PR/"))
        {
            var purchaseReturn = await WorkspaceScope.ApplyTo(db.PurchaseReturns.AsNoTracking(), context)
                .OrderByDescending(item => item.OnDate)
                .FirstOrDefaultAsync(item => item.ReturnNumber.ToUpper() == normalized || item.ReturnNumber.ToUpper().EndsWith(idPart), cancellationToken);
            if (purchaseReturn is not null)
            {
                return Results.Ok(PurchaseReturnResult(normalized, purchaseReturn));
            }
        }

        var salaryPayment = await WorkspaceScope.ApplyTo(db.SalaryPayments.AsNoTracking(), context)
            .Include(item => item.Employee)
            .OrderByDescending(item => item.OnDate)
            .FirstOrDefaultAsync(item => item.VoucherNumber.ToUpper() == normalized || item.VoucherNumber.ToUpper().EndsWith(idPart), cancellationToken);
        if (salaryPayment is not null)
        {
            return Results.Ok(new ScanLookupResult(normalized, "SalaryPayment", salaryPayment.Id, salaryPayment.VoucherNumber, salaryPayment.OnDate, salaryPayment.Employee != null ? salaryPayment.Employee.StaffName : "Employee", salaryPayment.Amount, $"/payroll?paymentId={salaryPayment.Id}"));
        }

        return Results.NotFound(new { message = "No permitted Garmetix document matched this scanned code." });
    }

    private static async Task<ScanLookupResult?> FindByTokenAsync(
        string documentType,
        Guid id,
        string code,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (documentType == DocumentCodeService.SaleInvoice)
        {
            var entity = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : new(code, "SaleInvoice", entity.Id, entity.InvoiceNumber, entity.OnDate, entity.CustomerName ?? "Walk-in Customer", entity.BillAmount, $"/billing?invoiceId={entity.Id}");
        }

        if (documentType == DocumentCodeService.PurchaseInvoice)
        {
            var entity = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : new(code, "PurchaseInvoice", entity.Id, entity.InvoiceNumber, entity.OnDate, entity.VendorName ?? "Supplier", entity.BillAmount, $"/purchase?invoiceId={entity.Id}");
        }

        if (documentType == DocumentCodeService.PurchaseReturn)
        {
            var entity = await WorkspaceScope.ApplyTo(db.PurchaseReturns.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : PurchaseReturnResult(code, entity);
        }

        if (documentType == DocumentCodeService.Voucher)
        {
            var entity = await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : new(code, "Voucher", entity.Id, entity.VoucherNumber, entity.OnDate, entity.PartyName, entity.Amount, $"/vouchers?voucherId={entity.Id}");
        }

        if (documentType == DocumentCodeService.CashVoucher)
        {
            var entity = await WorkspaceScope.ApplyTo(db.CashVouchers.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : new(code, "CashVoucher", entity.Id, entity.VoucherNumber, entity.OnDate, entity.PartyName, entity.Amount, $"/cash-vouchers?voucherId={entity.Id}");
        }

        if (documentType == DocumentCodeService.CommercialNote)
        {
            var entity = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : NoteResult(code, entity);
        }

        if (documentType == DocumentCodeService.PettyCash)
        {
            var entity = await WorkspaceScope.ApplyTo(db.PettyCashSheets.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : new(code, "PettyCashSheet", entity.Id, $"PETTY-{entity.OnDate:yyyyMMdd}", entity.OnDate, entity.CreatedBy ?? "Daily Cash", entity.CashInHand, $"/petty-cash?sheetId={entity.Id}");
        }

        if (documentType == DocumentCodeService.Payslip)
        {
            var entity = await WorkspaceScope.ApplyTo(db.SalaryPaySlips.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity is null)
            {
                return null;
            }

            var employeeName = await db.Employees.AsNoTracking()
                .Where(item => item.Id == entity.EmployeeId)
                .Select(item => item.StaffName)
                .FirstOrDefaultAsync(cancellationToken) ?? "Employee";
            return new(code, "SalaryPayslip", entity.Id, entity.MonthYear, entity.PayPeriodStart, employeeName, entity.NetSalary, $"/payroll?payslipId={entity.Id}");
        }

        if (documentType == DocumentCodeService.SalaryPayment)
        {
            var entity = await WorkspaceScope.ApplyTo(db.SalaryPayments.AsNoTracking(), context)
                .Include(item => item.Employee)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : new(code, "SalaryPayment", entity.Id, entity.VoucherNumber, entity.OnDate, entity.Employee?.StaffName ?? "Employee", entity.Amount, $"/payroll?paymentId={entity.Id}");
        }

        if (documentType == DocumentCodeService.NonGstGoods)
        {
            var entity = await WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : NonGstResult(code, entity);
        }

        return null;
    }

    private static ScanLookupResult NonGstResult(string code, Garmetix.Core.Models.Inventory.NonGstGoodsDocument document)
        => new(
            code,
            document.DocumentType == Garmetix.Core.Enums.NonGstGoodsDocumentType.Purchase ? "OffBookPurchase" : "OffBookSale",
            document.Id,
            document.DocumentNumber,
            document.OnDate,
            document.PartyName,
            document.NetAmount,
            $"/non-gst-goods?documentId={document.Id}");

    private static ScanLookupResult NoteResult(string code, Garmetix.Core.Models.Inventory.CommercialNote note)
        => new(
            code,
            note.NoteType.ToString(),
            note.Id,
            note.NoteNumber,
            note.OnDate,
            note.PartyName,
            note.Amount,
            note.NoteType == Garmetix.Core.Enums.NoteType.CreditNote ? $"/credit-notes/{note.Id}" : $"/debit-notes/{note.Id}");

    private static ScanLookupResult PurchaseReturnResult(string code, Garmetix.Core.Models.Inventory.PurchaseReturn purchaseReturn)
        => new(
            code,
            "PurchaseReturn",
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber,
            purchaseReturn.OnDate,
            purchaseReturn.VendorName,
            purchaseReturn.ReturnAmount,
            $"/purchase-return?returnId={purchaseReturn.Id}");
}
