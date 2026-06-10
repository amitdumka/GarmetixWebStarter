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

        return Results.NotFound(new { message = "No invoice or voucher matched this scanned code." });
    }
}
