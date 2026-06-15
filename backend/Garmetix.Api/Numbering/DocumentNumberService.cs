using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Numbering;

/// <summary>
/// Generates document numbers using a transaction-scoped PostgreSQL advisory lock plus
/// a persistent sequence row. This replaces count + 1 numbering, which can duplicate
/// when two users create bills at the same time.
/// </summary>
public sealed class DocumentNumberService(GarmetixDbContext db)
{
    public Task<string> NextSaleInvoiceAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
        => DocumentNumberGenerator.NextAsync(db, companyId, storeGroupId, storeId, "SalesInvoice", "S", DateTime.Today, cancellationToken);

    public Task<string> NextSalesReturnAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
        => DocumentNumberGenerator.NextAsync(db, companyId, storeGroupId, storeId, "SalesReturn", "SR", DateTime.Today, cancellationToken);

    public Task<string> NextSalesExchangeAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
        => DocumentNumberGenerator.NextAsync(db, companyId, storeGroupId, storeId, "SalesExchange", "EX", DateTime.Today, cancellationToken);

    public Task<string> NextPurchaseInvoiceAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
        => DocumentNumberGenerator.NextAsync(db, companyId, storeGroupId, storeId, "PurchaseInvoice", "P", DateTime.Today, cancellationToken);

    public Task<string> NextPurchaseInwardAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
        => DocumentNumberGenerator.NextAsync(db, companyId, storeGroupId, storeId, "PurchaseInward", "INW", DateTime.Today, cancellationToken);

    public Task<string> NextPurchaseReturnAsync(Guid companyId, Guid storeGroupId, Guid storeId, DateTime onDate, CancellationToken cancellationToken)
        => NextStoreMonthlyAsync(companyId, storeGroupId, storeId, "PurchaseReturn", "PR", onDate, cancellationToken);

    public Task<string> NextVendorPaymentVoucherAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
        => DocumentNumberGenerator.NextAsync(db, companyId, storeGroupId, storeId, "VendorPaymentVoucher", "PV", DateTime.Today, cancellationToken);

    public Task<string> NextStockAdjustmentAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
        => DocumentNumberGenerator.NextAsync(db, companyId, storeGroupId, storeId, "StockAdjustment", "ADJ", DateTime.Today, cancellationToken);

    public Task<string> NextStockTransferAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
        => DocumentNumberGenerator.NextAsync(db, companyId, storeGroupId, storeId, "StockTransfer", "ST", DateTime.Today, cancellationToken);

    public Task<string> NextPhysicalStockCountAsync(Guid companyId, Guid storeGroupId, Guid storeId, CancellationToken cancellationToken)
        => DocumentNumberGenerator.NextAsync(db, companyId, storeGroupId, storeId, "PhysicalStockCount", "PHY", DateTime.Today, cancellationToken);

    public Task<string> NextNonGstPurchaseAsync(Guid companyId, Guid storeGroupId, Guid storeId, DateTime onDate, CancellationToken cancellationToken)
        => NextStoreMonthlyAsync(companyId, storeGroupId, storeId, "NonGstPurchase", "NGP", onDate, cancellationToken);

    public Task<string> NextNonGstSaleAsync(Guid companyId, Guid storeGroupId, Guid storeId, DateTime onDate, CancellationToken cancellationToken)
        => NextStoreMonthlyAsync(companyId, storeGroupId, storeId, "NonGstSale", "NGS", onDate, cancellationToken);

    public async Task<string> NextVoucherAsync(
        Guid companyId,
        Guid storeGroupId,
        Guid storeId,
        VoucherType voucherType,
        DateTime onDate,
        CancellationToken cancellationToken)
    {
        var storeCode = await db.Stores.AsNoTracking()
            .Where(store => store.Id == storeId && store.CompanyId == companyId && store.StoreGroupId == storeGroupId)
            .Select(store => store.StoreCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(storeCode))
        {
            throw new InvalidOperationException("The selected store has no store code. Set the store code in Company setup.");
        }

        var sequenceMonth = new DateTime(onDate.Year, onDate.Month, 1);
        var sequence = await DocumentNumberGenerator.NextAsync(
            db, companyId, storeGroupId, storeId, "Voucher", "VCH", sequenceMonth, cancellationToken);
        var numericPart = sequence.Split('-').Last();
        var safeStoreCode = new string(storeCode.Trim().ToUpperInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return $"{(safeStoreCode.Length > 0 ? safeStoreCode : "STORE")}/{onDate:yyyyMM}/{numericPart}";
    }

    public async Task<string> NextSalaryPaymentAsync(
        Guid companyId,
        Guid storeGroupId,
        Guid storeId,
        DateTime onDate,
        CancellationToken cancellationToken)
    {
        var storeCode = await db.Stores.AsNoTracking()
            .Where(store => store.Id == storeId && store.CompanyId == companyId && store.StoreGroupId == storeGroupId)
            .Select(store => store.StoreCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(storeCode))
        {
            throw new InvalidOperationException("The selected store has no store code. Set the store code in Company setup.");
        }

        var sequenceMonth = new DateTime(onDate.Year, onDate.Month, 1);
        var sequence = await DocumentNumberGenerator.NextAsync(
            db, companyId, storeGroupId, storeId, "SalaryPayment", "SPAY", sequenceMonth, cancellationToken);
        var numericPart = sequence.Split('-').Last();
        var safeStoreCode = new string(storeCode.Trim().ToUpperInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return $"{(safeStoreCode.Length > 0 ? safeStoreCode : "STORE")}/{onDate:yyyyMM}/SPAY/{numericPart}";
    }

    private async Task<string> NextStoreMonthlyAsync(
        Guid companyId,
        Guid storeGroupId,
        Guid storeId,
        string documentType,
        string prefix,
        DateTime onDate,
        CancellationToken cancellationToken)
    {
        var storeCode = await db.Stores.AsNoTracking()
            .Where(store => store.Id == storeId && store.CompanyId == companyId && store.StoreGroupId == storeGroupId)
            .Select(store => store.StoreCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(storeCode))
        {
            throw new InvalidOperationException("The selected store has no store code. Set the store code in Company setup.");
        }

        var sequenceMonth = new DateTime(onDate.Year, onDate.Month, 1);
        var sequence = await DocumentNumberGenerator.NextAsync(
            db, companyId, storeGroupId, storeId, documentType, prefix, sequenceMonth, cancellationToken);
        var numericPart = sequence.Split('-').Last();
        var safeStoreCode = new string(storeCode.Trim().ToUpperInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return $"{(safeStoreCode.Length > 0 ? safeStoreCode : "STORE")}/{onDate:yyyyMM}/{prefix}/{numericPart}";
    }
}

public static class DocumentNumberGenerator
{
    private const int SequenceLockNamespace = 140000;
    private const int StockLockNamespace = 140001;

    public static async Task<string> NextAsync(
        GarmetixDbContext db,
        Guid companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string documentType,
        string prefix,
        DateTime documentDate,
        CancellationToken cancellationToken)
    {
        var sequenceDate = documentDate.Date;
        var lockKey = $"seq|{companyId:N}|{storeGroupId?.ToString("N") ?? "company"}|{storeId?.ToString("N") ?? "company"}|{documentType}|{sequenceDate:yyyyMMdd}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({SequenceLockNamespace}, hashtext({lockKey}));", cancellationToken);

        var sequence = await db.DocumentSequences.FirstOrDefaultAsync(item =>
            item.CompanyId == companyId &&
            item.StoreGroupId == storeGroupId &&
            item.StoreId == storeId &&
            item.DocumentType == documentType &&
            item.SequenceDate == sequenceDate,
            cancellationToken);

        if (sequence is null)
        {
            sequence = new DocumentSequence
            {
                CompanyId = companyId,
                StoreGroupId = storeGroupId,
                StoreId = storeId,
                DocumentType = documentType,
                Prefix = prefix,
                SequenceDate = sequenceDate,
                LastNumber = 1
            };
            db.DocumentSequences.Add(sequence);
        }
        else
        {
            sequence.LastNumber += 1;
            sequence.Prefix = prefix;
        }

        return $"{prefix}-{sequenceDate:yyyyMMdd}-{sequence.LastNumber:0000}";
    }

    public static Task LockStockKeyAsync(
        GarmetixDbContext db,
        Guid companyId,
        Guid storeGroupId,
        Guid storeId,
        Guid productId,
        string barcode,
        CancellationToken cancellationToken)
    {
        var cleanBarcode = string.IsNullOrWhiteSpace(barcode) ? "-" : barcode.Trim();
        var lockKey = $"stock|{companyId:N}|{storeGroupId:N}|{storeId:N}|{productId:N}|{cleanBarcode}";
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({StockLockNamespace}, hashtext({lockKey}));", cancellationToken);
    }
}
