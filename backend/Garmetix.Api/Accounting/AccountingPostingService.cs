using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Accounting;

public sealed class AccountingPostingService(GarmetixDbContext db)
{
    private static readonly PaymentMode[] BankPaymentModes =
    [
        PaymentMode.Card,
        PaymentMode.UPI,
        PaymentMode.Wallets,
        PaymentMode.IMPS,
        PaymentMode.RTGS,
        PaymentMode.NEFT,
        PaymentMode.Cheque,
        PaymentMode.DemandDraft
    ];

    public async Task<AccountingPostResult> SaveVoucherAsync(
        VoucherSaveRequest request,
        CancellationToken cancellationToken)
    {
        ValidateVoucher(request);

        var employeeExists = await db.Employees.AnyAsync(item => item.Id == request.EmployeeId, cancellationToken);
        if (!employeeExists)
        {
            throw new InvalidOperationException("Select a valid employee in Issued by.");
        }

        var ledger = await db.Ledgers.FirstOrDefaultAsync(item => item.Id == request.LedgerId, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid ledger.");

        var bankAccount = await ResolveBankAccountAsync(request, cancellationToken);
        var cashOrBankLedger = await ResolveCashOrBankLedgerAsync(request, bankAccount, cancellationToken);

        var voucher = await ResolveVoucherAsync(request, cancellationToken);
        voucher.VoucherNumber = request.VoucherNumber.Trim();
        voucher.OnDate = request.OnDate;
        voucher.VoucherType = request.VoucherType;
        voucher.PartyName = request.PartyName.Trim();
        voucher.Particulars = request.Particulars.Trim();
        voucher.Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        voucher.Remarks = request.Remarks?.Trim() ?? string.Empty;
        voucher.SlipNumber = request.SlipNumber?.Trim();
        voucher.PaymentMode = request.PaymentMode;
        voucher.PaymentDetails = request.PaymentDetails?.Trim();
        voucher.IsParty = request.IsParty || request.PartyId.HasValue;
        voucher.PartyId = NormalizeGuid(request.PartyId);
        voucher.LedgerId = ledger.Id;
        voucher.EmployeeId = request.EmployeeId;
        voucher.AccountNumber = bankAccount?.Id;
        voucher.CompanyId = request.CompanyId;
        voucher.StoreGroupId = request.StoreGroupId;
        voucher.StoreId = request.StoreId;

        await db.SaveChangesAsync(cancellationToken);

        var (debitLedgerId, creditLedgerId) = GetVoucherLegs(request.VoucherType, ledger.Id, cashOrBankLedger.Id);
        var journal = await RepostJournalAsync(voucher, debitLedgerId, creditLedgerId, request.EmployeeId, cancellationToken);
        var bankTransaction = await UpsertBankTransactionAsync(voucher, bankAccount, cancellationToken);
        var chequeLog = await UpsertChequeLogAsync(voucher, bankAccount, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        if (bankAccount is not null)
        {
            await RecalculateBankStatementBalancesAsync(bankAccount.Id, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new AccountingPostResult(
            voucher.Id,
            journal.Id,
            debitLedgerId,
            creditLedgerId,
            bankTransaction?.Id,
            chequeLog?.Id);
    }

    public async Task<List<TrialBalanceRow>> GetTrialBalanceAsync(Guid? companyId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var query = db.JournalLines.AsNoTracking().AsQueryable();
        if (companyId.HasValue)
        {
            query = query.Where(line => line.CompanyId == companyId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(line => line.JournalEntry != null && line.JournalEntry.OnDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(line => line.JournalEntry != null && line.JournalEntry.OnDate <= to.Value);
        }

        var lines = await query.ToListAsync(cancellationToken);
        var ledgerIds = lines.Select(line => line.LedgerId).Distinct().ToList();
        var ledgers = await db.Ledgers.AsNoTracking()
            .Where(ledger => ledgerIds.Contains(ledger.Id))
            .ToListAsync(cancellationToken);
        var groupIds = ledgers.Select(ledger => ledger.LedgerGroupId).Distinct().ToList();
        var groups = await db.LedgerGroups.AsNoTracking()
            .Where(group => groupIds.Contains(group.Id))
            .ToDictionaryAsync(group => group.Id, cancellationToken);

        return lines
            .GroupBy(line => line.LedgerId)
            .Select(group =>
            {
                var ledger = ledgers.First(item => item.Id == group.Key);
                groups.TryGetValue(ledger.LedgerGroupId, out var ledgerGroup);
                var debit = group.Sum(item => item.Debit);
                var credit = group.Sum(item => item.Credit);
                var balance = debit - credit;

                return new TrialBalanceRow(
                    ledger.Id,
                    ledger.Name,
                    ledgerGroup?.Name ?? "Ledger Group",
                    debit,
                    credit,
                    balance >= 0 ? balance : 0,
                    balance < 0 ? Math.Abs(balance) : 0);
            })
            .OrderBy(row => row.LedgerGroup)
            .ThenBy(row => row.LedgerName)
            .ToList();
    }

    public async Task<List<BankStatementRow>> GetBankStatementAsync(Guid bankAccountId, CancellationToken cancellationToken)
    {
        return await db.BankStatementLines.AsNoTracking()
            .Where(line => line.BankAccountId == bankAccountId)
            .OrderByDescending(line => line.OnDate)
            .ThenByDescending(line => line.CreatedAt)
            .Select(line => new BankStatementRow(line.Id, line.OnDate, line.Description, line.Reference, line.Debit, line.Credit, line.Balance, line.Reconciled))
            .ToListAsync(cancellationToken);
    }

    public async Task<BankTransaction> SaveBankTransactionAsync(
        BankTransactionSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("Company is required.");
        }

        if (request.BankAccountId == Guid.Empty)
        {
            throw new ArgumentException("Bank account is required.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        var bankAccount = await db.BankAccounts.FirstOrDefaultAsync(item => item.Id == request.BankAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid bank account.");

        var transaction = request.Id.HasValue
            ? await db.BankTransactions.FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken)
            : null;

        transaction ??= new BankTransaction
        {
            CompanyId = request.CompanyId
        };

        transaction.CompanyId = request.CompanyId;
        transaction.BankAccountId = bankAccount.Id;
        transaction.OnDate = request.OnDate;
        transaction.TransactionType = request.TransactionType;
        transaction.TransactionMode = request.TransactionMode;
        transaction.Narration = request.Narration?.Trim() ?? string.Empty;
        transaction.Reference = string.IsNullOrWhiteSpace(request.Reference)
            ? $"BANK-{DateTime.Now:yyyyMMddHHmmss}"
            : request.Reference.Trim();
        transaction.Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        transaction.PersonName = request.PersonName?.Trim() ?? string.Empty;

        if (db.Entry(transaction).State == EntityState.Detached)
        {
            db.BankTransactions.Add(transaction);
        }

        await UpsertManualStatementLineAsync(transaction, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await RecalculateBankStatementBalancesAsync(bankAccount.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    private async Task<Voucher> ResolveVoucherAsync(VoucherSaveRequest request, CancellationToken cancellationToken)
    {
        if (request.Id.HasValue)
        {
            var existing = await db.Vouchers.FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        var voucher = new Voucher();
        db.Vouchers.Add(voucher);
        return voucher;
    }

    private async Task<BankAccount?> ResolveBankAccountAsync(VoucherSaveRequest request, CancellationToken cancellationToken)
    {
        if (!RequiresBank(request.PaymentMode))
        {
            return null;
        }

        if (!request.AccountNumber.HasValue)
        {
            throw new InvalidOperationException("Select bank account for non-cash payment mode.");
        }

        return await db.BankAccounts.FirstOrDefaultAsync(item => item.Id == request.AccountNumber.Value, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid bank account.");
    }

    private async Task<Ledger> ResolveCashOrBankLedgerAsync(
        VoucherSaveRequest request,
        BankAccount? bankAccount,
        CancellationToken cancellationToken)
    {
        if (bankAccount is not null)
        {
            return await db.Ledgers.FirstOrDefaultAsync(item => item.Id == bankAccount.LedgerId, cancellationToken)
                ?? throw new InvalidOperationException("The selected bank account is not linked to a ledger.");
        }

        var cashLedger = await db.Ledgers.FirstOrDefaultAsync(
            item => item.CompanyId == request.CompanyId && item.Name == "Cash In Hand",
            cancellationToken);

        if (cashLedger is null)
        {
            throw new InvalidOperationException("Cash In Hand ledger is missing. Run accounting defaults.");
        }

        return cashLedger;
    }

    private static (Guid DebitLedgerId, Guid CreditLedgerId) GetVoucherLegs(
        VoucherType type,
        Guid selectedLedgerId,
        Guid cashOrBankLedgerId)
    {
        return type == VoucherType.Receipt
            ? (cashOrBankLedgerId, selectedLedgerId)
            : (selectedLedgerId, cashOrBankLedgerId);
    }

    private async Task<JournalEntry> RepostJournalAsync(
        Voucher voucher,
        Guid debitLedgerId,
        Guid creditLedgerId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var existing = await db.JournalEntries
            .Include(entry => entry.Lines)
            .FirstOrDefaultAsync(entry => entry.SourceType == "Voucher" && entry.SourceId == voucher.Id, cancellationToken);

        if (existing is not null)
        {
            db.JournalLines.RemoveRange(existing.Lines ?? []);
            existing.OnDate = voucher.OnDate;
            existing.ReferenceNumber = voucher.VoucherNumber;
            existing.Narration = voucher.Particulars;
            existing.CompanyId = voucher.CompanyId;
            existing.StoreGroupId = voucher.StoreGroupId;
            existing.StoreId = voucher.StoreId;
            existing.PostedAt = DateTime.Now;
            existing.PostedBy = voucher.CreatedBy;
        }
        else
        {
            existing = new JournalEntry
            {
                EntryNumber = $"JE-{voucher.VoucherNumber}",
                OnDate = voucher.OnDate,
                SourceType = "Voucher",
                SourceId = voucher.Id,
                ReferenceNumber = voucher.VoucherNumber,
                Narration = voucher.Particulars,
                CompanyId = voucher.CompanyId,
                StoreGroupId = voucher.StoreGroupId,
                StoreId = voucher.StoreId,
                PostedBy = voucher.CreatedBy
            };
            db.JournalEntries.Add(existing);
        }

        db.JournalLines.AddRange(
            new JournalLine
            {
                JournalEntryId = existing.Id,
                LedgerId = debitLedgerId,
                PartyId = voucher.PartyId,
                EmployeeId = employeeId,
                Debit = voucher.Amount,
                Credit = 0,
                Narration = voucher.Particulars,
                CompanyId = voucher.CompanyId,
                StoreGroupId = voucher.StoreGroupId,
                StoreId = voucher.StoreId
            },
            new JournalLine
            {
                JournalEntryId = existing.Id,
                LedgerId = creditLedgerId,
                PartyId = voucher.PartyId,
                EmployeeId = employeeId,
                Debit = 0,
                Credit = voucher.Amount,
                Narration = voucher.Particulars,
                CompanyId = voucher.CompanyId,
                StoreGroupId = voucher.StoreGroupId,
                StoreId = voucher.StoreId
            });

        return existing;
    }

    private async Task<BankTransaction?> UpsertBankTransactionAsync(
        Voucher voucher,
        BankAccount? bankAccount,
        CancellationToken cancellationToken)
    {
        var existing = await db.BankTransactions.FirstOrDefaultAsync(
            item => item.Reference == voucher.VoucherNumber,
            cancellationToken);

        if (bankAccount is null)
        {
            if (existing is not null)
            {
                db.BankTransactions.Remove(existing);
            }

            return null;
        }

        var transactionType = voucher.VoucherType == VoucherType.Receipt ? TransactionType.Deposit : TransactionType.Withdraw;
        var mode = ToTransactionMode(voucher.PaymentMode);

        existing ??= new BankTransaction
        {
            CompanyId = voucher.CompanyId
        };

        existing.BankAccountId = bankAccount.Id;
        existing.OnDate = voucher.OnDate;
        existing.TransactionType = transactionType;
        existing.TransactionMode = mode;
        existing.Narration = voucher.Particulars;
        existing.Reference = voucher.VoucherNumber;
        existing.Amount = voucher.Amount;
        existing.PersonName = voucher.PartyName;

        if (existing.Id == Guid.Empty || db.Entry(existing).State == EntityState.Detached)
        {
            db.BankTransactions.Add(existing);
        }

        await UpsertStatementLineAsync(existing, voucher, transactionType, cancellationToken);
        return existing;
    }

    private async Task UpsertStatementLineAsync(
        BankTransaction bankTransaction,
        Voucher voucher,
        TransactionType transactionType,
        CancellationToken cancellationToken)
    {
        var statement = await db.BankStatementLines.FirstOrDefaultAsync(
            item => item.BankTransactionId == bankTransaction.Id,
            cancellationToken);

        statement ??= new BankStatementLine
        {
            BankTransactionId = bankTransaction.Id,
            CompanyId = voucher.CompanyId
        };

        statement.BankAccountId = bankTransaction.BankAccountId;
        statement.OnDate = voucher.OnDate;
        statement.ValueDate = voucher.OnDate;
        statement.Description = voucher.Particulars;
        statement.Reference = voucher.VoucherNumber;
        statement.Debit = transactionType == TransactionType.Withdraw ? voucher.Amount : 0;
        statement.Credit = transactionType == TransactionType.Deposit ? voucher.Amount : 0;
        statement.Balance = 0;

        if (statement.Id == Guid.Empty || db.Entry(statement).State == EntityState.Detached)
        {
            db.BankStatementLines.Add(statement);
        }
    }

    private async Task UpsertManualStatementLineAsync(
        BankTransaction transaction,
        CancellationToken cancellationToken)
    {
        var statement = await db.BankStatementLines.FirstOrDefaultAsync(
            item => item.BankTransactionId == transaction.Id,
            cancellationToken);

        statement ??= new BankStatementLine
        {
            BankTransactionId = transaction.Id,
            CompanyId = transaction.CompanyId
        };

        statement.CompanyId = transaction.CompanyId;
        statement.BankAccountId = transaction.BankAccountId;
        statement.OnDate = transaction.OnDate;
        statement.ValueDate = transaction.OnDate;
        statement.Description = transaction.Narration ?? string.Empty;
        statement.Reference = transaction.Reference;
        statement.Debit = transaction.TransactionType == TransactionType.Withdraw ? transaction.Amount : 0;
        statement.Credit = transaction.TransactionType == TransactionType.Deposit ? transaction.Amount : 0;

        if (db.Entry(statement).State == EntityState.Detached)
        {
            db.BankStatementLines.Add(statement);
        }
    }

    private async Task<ChequeLog?> UpsertChequeLogAsync(
        Voucher voucher,
        BankAccount? bankAccount,
        CancellationToken cancellationToken)
    {
        var existing = await db.ChequeLogs.FirstOrDefaultAsync(
            item => item.Narration == voucher.VoucherNumber,
            cancellationToken);

        if (voucher.PaymentMode != PaymentMode.Cheque || bankAccount is null)
        {
            if (existing is not null)
            {
                db.ChequeLogs.Remove(existing);
            }

            return null;
        }

        existing ??= new ChequeLog
        {
            CompanyId = voucher.CompanyId
        };

        existing.BankAccountId = bankAccount.Id;
        existing.ChequeNumber = string.IsNullOrWhiteSpace(voucher.SlipNumber) ? voucher.VoucherNumber : voucher.SlipNumber;
        existing.CheequeNumber = existing.ChequeNumber;
        existing.OnDate = voucher.OnDate;
        existing.ChequeDate = voucher.OnDate;
        existing.Narration = voucher.VoucherNumber;
        existing.ChequeBank = bankAccount.AccountNumber;
        existing.Amount = voucher.Amount;
        existing.PersonName = voucher.PartyName;
        existing.Status = voucher.VoucherType == VoucherType.Receipt ? "Deposited" : "Issued";
        existing.InHouse = false;

        if (existing.Id == Guid.Empty || db.Entry(existing).State == EntityState.Detached)
        {
            db.ChequeLogs.Add(existing);
        }

        return existing;
    }

    public async Task RecalculateBankStatementBalancesAsync(
        Guid bankAccountId,
        CancellationToken cancellationToken)
    {
        var bankAccount = await db.BankAccounts.FirstOrDefaultAsync(item => item.Id == bankAccountId, cancellationToken);
        if (bankAccount is null)
        {
            return;
        }

        var balance = bankAccount.OpeningBalance;
        var statements = await db.BankStatementLines
            .Where(item => item.BankAccountId == bankAccountId)
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        foreach (var statement in statements)
        {
            balance += statement.Credit - statement.Debit;
            statement.Balance = balance;
        }

        bankAccount.ClosingBalance = balance;
    }

    private static TransactionMode ToTransactionMode(PaymentMode mode)
    {
        return mode switch
        {
            PaymentMode.Cheque => TransactionMode.Cheque,
            PaymentMode.NEFT => TransactionMode.NEFT,
            PaymentMode.RTGS => TransactionMode.RTGS,
            PaymentMode.IMPS => TransactionMode.IMPS,
            PaymentMode.UPI => TransactionMode.UPI,
            PaymentMode.Card => TransactionMode.Swipe,
            PaymentMode.DemandDraft => TransactionMode.DD,
            _ => TransactionMode.Other
        };
    }

    private static bool RequiresBank(PaymentMode mode)
    {
        return BankPaymentModes.Contains(mode);
    }

    private static Guid? NormalizeGuid(Guid? value)
    {
        return value.HasValue && value.Value != Guid.Empty ? value.Value : null;
    }

    private static void ValidateVoucher(VoucherSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VoucherNumber))
        {
            throw new ArgumentException("Voucher number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PartyName))
        {
            throw new ArgumentException("Party name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Particulars))
        {
            throw new ArgumentException("Particulars are required.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        if (request.CompanyId == Guid.Empty || request.StoreGroupId == Guid.Empty || request.StoreId == Guid.Empty)
        {
            throw new ArgumentException("Company, store group, and store are required.");
        }
    }
}
