using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Accounting;

/// <summary>One store day's cash movements, as used by both Day Close and the Petty Cash Sheet.</summary>
public sealed record DayCashMovements(
    decimal Sales,
    decimal Receipts,
    decimal DueReceipts,
    decimal BankWithdrawal,
    decimal Expenses,
    decimal Payments,
    decimal CustomerDue,
    decimal BankDeposit,
    decimal NonCashSale,
    decimal SalaryPayments,
    decimal CashRefunds)
{
    public decimal CashInHand(decimal openingBalance)
        => DayCashCalculator.CashInHand(openingBalance, Sales, Receipts, DueReceipts, BankWithdrawal,
            Expenses, Payments, CustomerDue, BankDeposit, NonCashSale, SalaryPayments);

    public IEnumerable<string> Notes()
    {
        if (SalaryPayments > 0)
        {
            yield return $"Includes {SalaryPayments:0.00} in cash salary payments made today (from Salary Payment records), already subtracted from cash in hand.";
        }

        if (CashRefunds > 0)
        {
            yield return $"Payments include {CashRefunds:0.00} in cash refunds paid on sales returns today, already subtracted from cash in hand.";
        }
    }
}

/// <summary>
/// The single source for the day's expected cash. The opening balance is left to the caller:
/// Day Close and the Petty Cash Sheet pick it by different rules.
/// </summary>
public static class DayCashCalculator
{
    public static decimal CashInHand(
        decimal openingBalance,
        decimal sales,
        decimal receipts,
        decimal dueReceipts,
        decimal bankWithdrawal,
        decimal expenses,
        decimal payments,
        decimal customerDue,
        decimal bankDeposit,
        decimal nonCashSale,
        decimal salaryPayments)
        => Round(openingBalance + sales + receipts + dueReceipts + bankWithdrawal
            - expenses - payments - customerDue - bankDeposit - nonCashSale - salaryPayments);

    public static async Task<DayCashMovements> LoadAsync(
        GarmetixDbContext db,
        Guid storeId,
        DateTime onDate,
        CancellationToken cancellationToken)
    {
        var dayStart = onDate.Date;
        var dayEnd = dayStart.AddDays(1);

        var invoicePayments = await db.InvoicePayments.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd)
            .Select(item => new { item.InvoiceId, item.PaymentMode, item.Amount })
            .ToListAsync(cancellationToken);

        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd && !item.ReturnInvoice)
            .Select(item => new { item.Id, item.BillAmount, item.PaidAmount, item.CreditSale })
            .ToListAsync(cancellationToken);
        var currentInvoiceIds = invoices.Select(item => item.Id).ToHashSet();

        // A CASH REFUND on a sales-return invoice is money paid out, not a receipt: keep it out of dueReceipts and
        // count it with payments (the return invoice itself is excluded from `invoices`).
        var returnInvoiceIds = (await db.SalesInvoices.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd && item.ReturnInvoice)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        var cashRefunds = invoicePayments
            .Where(item => item.PaymentMode == PaymentMode.Cash && returnInvoiceIds.Contains(item.InvoiceId))
            .Sum(item => item.Amount);
        // Held store credit paid out in cash (Billing > store-credit-refund) is also money leaving the till.
        cashRefunds += await db.JournalLines.AsNoTracking()
            .Where(line => line.JournalEntry!.SourceType == "StoreCreditRefund" && line.JournalEntry.StoreId == storeId
                && line.JournalEntry.OnDate >= dayStart && line.JournalEntry.OnDate < dayEnd && line.Ledger!.Name == "Cash In Hand")
            .SumAsync(line => line.Credit, cancellationToken);

        var dueReceipts = invoicePayments
            .Where(item => item.PaymentMode == PaymentMode.Cash && !currentInvoiceIds.Contains(item.InvoiceId) && !returnInvoiceIds.Contains(item.InvoiceId))
            .Sum(item => item.Amount);
        var nonCashSales = invoicePayments
            .Where(item => currentInvoiceIds.Contains(item.InvoiceId) && item.PaymentMode != PaymentMode.Cash)
            .Sum(item => item.Amount);
        var customerDue = invoices
            .Where(item => item.CreditSale || item.BillAmount > item.PaidAmount)
            .Sum(item => Math.Max(0, item.BillAmount - item.PaidAmount));

        var vouchers = await db.Vouchers.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd && item.PaymentMode == PaymentMode.Cash)
            .Select(item => new { item.VoucherType, item.Amount })
            .ToListAsync(cancellationToken);
        var cashVouchers = await db.CashVouchers.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd)
            .Select(item => new { item.VoucherType, item.Amount })
            .ToListAsync(cancellationToken);

        decimal VoucherTotal(VoucherType type)
            => vouchers.Where(item => item.VoucherType == type).Sum(item => item.Amount)
               + cashVouchers.Where(item => item.VoucherType == type).Sum(item => item.Amount);

        var bankCash = await db.BankCashTranscations.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd)
            .Select(item => new { item.TransactionType, item.Amount })
            .ToListAsync(cancellationToken);

        var salaryPayments = await db.SalaryPayments.AsNoTracking()
            .Where(item => item.StoreId == storeId && !item.Deleted && item.PaymentMode == PaymentMode.Cash
                && item.OnDate >= dayStart && item.OnDate < dayEnd)
            .SumAsync(item => item.Amount, cancellationToken);

        return new DayCashMovements(
            Sales: Round(invoices.Sum(item => item.BillAmount)),
            Receipts: Round(VoucherTotal(VoucherType.Receipt)),
            DueReceipts: Round(dueReceipts),
            BankWithdrawal: Round(bankCash.Where(item => item.TransactionType == TransactionType.Withdraw).Sum(item => item.Amount)),
            Expenses: Round(VoucherTotal(VoucherType.Expense)),
            Payments: Round(VoucherTotal(VoucherType.Payment) + cashRefunds),
            CustomerDue: Round(customerDue),
            BankDeposit: Round(bankCash.Where(item => item.TransactionType == TransactionType.Deposit).Sum(item => item.Amount)),
            NonCashSale: Round(nonCashSales),
            SalaryPayments: Round(salaryPayments),
            CashRefunds: Round(cashRefunds));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
