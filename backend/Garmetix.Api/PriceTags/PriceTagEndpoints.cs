using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.PriceTags;

public static class PriceTagEndpoints
{
    public static RouteGroupBuilder MapPriceTagEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/price-tags")
            .WithTags("Price Tag Printing")
            .RequireAuthorization(GarmetixPolicies.Inventory);

        group.MapGet("/search", SearchStockAsync);
        group.MapGet("/purchase-inwards", SearchPurchaseInwardsAsync);
        group.MapGet("/purchase-inwards/{id:guid}/items", PurchaseInwardItemsAsync);
        group.MapGet("/import-batches/{id:guid}/items", PurchaseImportBatchItemsAsync);
        group.MapPost("/prepare", PrepareAsync);

        return group;
    }

    private static async Task<IResult> SearchStockAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? storeId,
        string? query,
        bool inStockOnly = true,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var normalized = (query ?? string.Empty).Trim();
        var limit = Math.Clamp(take, 1, 200);

        var stockQuery = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(stock => !stock.IsOFB)
            .Include(stock => stock.Product)
                .ThenInclude(product => product!.ProductCategory)
            .Include(stock => stock.Product)
                .ThenInclude(product => product!.ProductSubCategory)
            .AsQueryable();

        if (storeId.HasValue)
        {
            stockQuery = stockQuery.Where(stock => stock.StoreId == storeId.Value);
        }

        if (inStockOnly)
        {
            stockQuery = stockQuery.Where(stock => stock.PurchaseQty - stock.SoldQty > 0);
        }

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var lowered = normalized.ToLower();
            stockQuery = stockQuery.Where(stock =>
                stock.Barcode.ToLower().Contains(lowered) ||
                (stock.HSNCode != null && stock.HSNCode.ToLower().Contains(lowered)) ||
                (stock.Product != null && stock.Product.Name.ToLower().Contains(lowered)) ||
                (stock.Product != null && stock.Product.Barcode.ToLower().Contains(lowered)));
        }

        var rows = await stockQuery
            .OrderBy(stock => stock.Product != null ? stock.Product.Name : stock.Barcode)
            .ThenBy(stock => stock.Barcode)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var storeIds = rows.Select(row => row.StoreId).Distinct().ToArray();
        var storeNames = await db.Stores.AsNoTracking()
            .Where(store => storeIds.Contains(store.Id))
            .ToDictionaryAsync(store => store.Id, store => store.Name, cancellationToken);

        var productIds = rows.Select(row => row.ProductId).Distinct().ToArray();
        var detailRows = await db.ProductDetails.AsNoTracking()
            .Where(detail => productIds.Contains(detail.ProductId))
            .Select(detail => new { detail.ProductId, detail.Brand })
            .ToListAsync(cancellationToken);
        var details = detailRows
            .GroupBy(detail => detail.ProductId)
            .ToDictionary(group => group.Key, group => group.Select(detail => detail.Brand).FirstOrDefault(brand => !string.IsNullOrWhiteSpace(brand)));

        return Results.Ok(rows.Select(stock => ToRow(stock, storeNames.GetValueOrDefault(stock.StoreId), details.GetValueOrDefault(stock.ProductId))).ToList());
    }

    private static async Task<IResult> SearchPurchaseInwardsAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? storeId,
        string? query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var normalized = (query ?? string.Empty).Trim();
        var limit = Math.Clamp(take, 1, 100);
        var purchaseQuery = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context).AsQueryable();

        if (storeId.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(invoice => invoice.StoreId == storeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var lowered = normalized.ToLower();
            purchaseQuery = purchaseQuery.Where(invoice =>
                invoice.InvoiceNumber.ToLower().Contains(lowered) ||
                invoice.InwardNumber.ToLower().Contains(lowered) ||
                (invoice.VendorName != null && invoice.VendorName.ToLower().Contains(lowered)) ||
                (invoice.VendorGSTIN != null && invoice.VendorGSTIN.ToLower().Contains(lowered)));
        }

        var rows = await purchaseQuery
            .OrderByDescending(invoice => invoice.InwardDate)
            .ThenByDescending(invoice => invoice.CreatedAt)
            .Take(limit)
            .Select(invoice => new PriceTagPurchaseInwardDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InwardNumber,
                invoice.VendorName ?? "Vendor",
                invoice.InwardDate,
                invoice.SupplierInvoiceDate,
                invoice.BillAmount))
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> PurchaseInwardItemsAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (invoice is null)
        {
            return Results.NotFound(new { message = "Purchase inward was not found." });
        }

        var items = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == id)
            .Include(item => item.Product)
                .ThenInclude(product => product!.ProductCategory)
            .Include(item => item.Product)
                .ThenInclude(product => product!.ProductSubCategory)
            .OrderBy(item => item.ProductName ?? item.Barcode)
            .ThenBy(item => item.Barcode)
            .ToListAsync(cancellationToken);

        var barcodes = items.Select(item => item.Barcode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var stocks = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(stock => stock.StoreId == invoice.StoreId && barcodes.Contains(stock.Barcode))
            .ToListAsync(cancellationToken);
        var stockByBarcode = stocks
            .GroupBy(stock => stock.Barcode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var storeName = await db.Stores.AsNoTracking()
            .Where(store => store.Id == invoice.StoreId)
            .Select(store => store.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var productIds = items.Select(row => row.ProductId).Distinct().ToArray();
        var detailRows = await db.ProductDetails.AsNoTracking()
            .Where(detail => productIds.Contains(detail.ProductId))
            .Select(detail => new { detail.ProductId, detail.Brand })
            .ToListAsync(cancellationToken);
        var details = detailRows
            .GroupBy(detail => detail.ProductId)
            .ToDictionary(group => group.Key, group => group.Select(detail => detail.Brand).FirstOrDefault(brand => !string.IsNullOrWhiteSpace(brand)));

        var rows = items.Select(item =>
        {
            stockByBarcode.TryGetValue(item.Barcode, out var stock);
            var mrp = stock?.MRP > 0 ? stock.MRP : item.MRP;
            var current = stock is null ? item.BilledQuantity : Math.Max(0, stock.PurchaseQty - stock.SoldQty);
            return new PriceTagStockRowDto(
                stock?.Id ?? Guid.Empty,
                item.ProductId,
                invoice.StoreId ?? Guid.Empty,
                item.ProductName ?? item.Product?.Name ?? item.Barcode,
                item.Barcode,
                item.HSNCode ?? item.Product?.HSNCode,
                item.Unit?.ToString() ?? item.Product?.Unit.ToString() ?? string.Empty,
                current,
                stock?.CostPrice ?? 0m,
                mrp,
                item.TaxPercentage,
                item.Product?.ProductCategory?.Name,
                item.Product?.ProductSubCategory?.Name,
                details.GetValueOrDefault(item.ProductId),
                storeName);
        }).ToList();

        return Results.Ok(rows);
    }


    private static async Task<IResult> PurchaseImportBatchItemsAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (batch is null)
        {
            return Results.NotFound(new { message = "Supplier invoice import batch was not found." });
        }

        if (!batch.PostedPurchaseInvoiceId.HasValue)
        {
            return Results.BadRequest(new { message = "Only posted supplier invoice imports can be loaded for price tag printing." });
        }

        return await PurchaseInwardItemsAsync(batch.PostedPurchaseInvoiceId.Value, context, db, cancellationToken);
    }

    private static IResult PrepareAsync(PriceTagRequest request)
    {
        var warnings = new List<string>();
        var labelSize = NormalizeLabelSize(request.LabelSize, warnings);
        var printerLanguage = NormalizePrinterLanguage(request.PrinterLanguage);
        var labels = new List<PriceTagLabelDto>();

        foreach (var row in request.Rows ?? Array.Empty<PriceTagRequestRow>())
        {
            var copies = Math.Clamp((int)Math.Ceiling(row.Copies <= 0 ? 1 : row.Copies), 1, 500);
            var productName = Clean(row.ProductName, 48);
            var barcode = Clean(row.Barcode, 32);
            if (string.IsNullOrWhiteSpace(barcode))
            {
                warnings.Add($"Skipped {productName}: barcode is missing.");
                continue;
            }

            for (var copy = 1; copy <= copies; copy++)
            {
                labels.Add(new PriceTagLabelDto(productName, barcode, Clean(row.HsnCode, 12), Clean(row.Brand, 20), Math.Max(0, row.Mrp), copy));
            }
        }

        var commands = printerLanguage == "tspl" ? BuildTspl(labelSize, labels, request.StoreName) : string.Empty;
        return Results.Ok(new PriceTagPreviewDto(labelSize, printerLanguage, labels.Count, labels, commands, warnings));
    }

    private static PriceTagStockRowDto ToRow(Garmetix.Core.Models.Inventory.Stock stock, string? storeName, string? brand)
        => new(
            stock.Id,
            stock.ProductId,
            stock.StoreId,
            stock.Product?.Name ?? stock.Barcode,
            stock.Barcode,
            stock.HSNCode ?? stock.Product?.HSNCode,
            stock.Unit.ToString(),
            Math.Max(0, stock.PurchaseQty - stock.SoldQty),
            stock.CostPrice,
            stock.MRP,
            stock.TaxRate,
            stock.Product?.ProductCategory?.Name,
            stock.Product?.ProductSubCategory?.Name,
            brand,
            storeName);

    private static string NormalizeLabelSize(string? value, List<string> warnings)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty);
        if (normalized is "50x25" or "50x25mm") return "50x25";
        if (normalized is "50x30" or "50x30mm") return "50x30";
        warnings.Add("Unknown tag size. Defaulted to 50x30 mm.");
        return "50x30";
    }

    private static string NormalizePrinterLanguage(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized == "tspl" ? "tspl" : "browser";
    }

    private static string BuildTspl(string labelSize, IReadOnlyList<PriceTagLabelDto> labels, string? storeName)
    {
        var height = labelSize == "50x25" ? 25 : 30;
        var sb = new StringBuilder();
        foreach (var label in labels)
        {
            sb.AppendLine($"SIZE 50 mm,{height} mm");
            sb.AppendLine("GAP 2 mm,0 mm");
            sb.AppendLine("DENSITY 8");
            sb.AppendLine("SPEED 4");
            sb.AppendLine("DIRECTION 1");
            sb.AppendLine("CLS");
            sb.AppendLine($"TEXT 16,8,\"2\",0,1,1,\"{Tspl(label.ProductName, 24)}\"");
            if (!string.IsNullOrWhiteSpace(label.Brand))
            {
                sb.AppendLine($"TEXT 16,30,\"1\",0,1,1,\"{Tspl(label.Brand, 22)}\"");
            }
            sb.AppendLine($"TEXT 250,30,\"2\",0,1,1,\"Rs {label.Mrp:0}\"");
            var barcodeTop = height == 25 ? 54 : 62;
            var barcodeHeight = height == 25 ? 44 : 52;
            sb.AppendLine($"BARCODE 36,{barcodeTop},\"128\",{barcodeHeight},1,0,2,2,\"{Tspl(label.Barcode, 30)}\"");
            sb.AppendLine($"TEXT 108,{barcodeTop + barcodeHeight + 6},\"1\",0,1,1,\"{Tspl(label.Barcode, 30)}\"");
            if (!string.IsNullOrWhiteSpace(storeName))
            {
                sb.AppendLine($"TEXT 16,{barcodeTop + barcodeHeight + 26},\"1\",0,1,1,\"{Tspl(storeName, 28)}\"");
            }
            sb.AppendLine("PRINT 1,1");
        }
        return sb.ToString();
    }

    private static string Tspl(string? value, int max)
        => Clean(value, max).Replace("\"", "'");

    private static string Clean(string? value, int max)
    {
        var cleaned = new string((value ?? string.Empty)
            .Where(ch => !char.IsControl(ch))
            .ToArray())
            .Trim();
        return cleaned.Length <= max ? cleaned : cleaned[..max];
    }
}
