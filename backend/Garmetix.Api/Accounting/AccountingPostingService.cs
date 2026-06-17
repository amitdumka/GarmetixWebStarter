using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Garmetix.Api.Numbering;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Accounting;

public sealed record VendorRefundPostingResult(Guid JournalEntryId, Guid? BankTransactionId);
public sealed record PurchaseReturnTaxPosting(Guid ReversalId, string ProductName, string? HsnCode, decimal TaxAmount);
public sealed record StockOperationAccountingResult(Guid JournalEntryId, string EntryNumber, decimal Amount);
public sealed record SalesInvoicePaymentPosting(
    PaymentMode PaymentMode,
    decimal Amount,
    Guid? BankAccountId,
    string? ReferenceNumber,
    string? GatewayReference,
    string? SettlementStatus,
    string? AdjustmentSourceType,
    Guid? AdjustmentSourceId,
    string? PaymentDetailsJson = null);

public sealed class AccountingPostingService(GarmetixDbContext db, DocumentNumberService documentNumbers)
{
    private sealed record JournalLineDraft(
        Guid LedgerId,
        Guid? PartyId,
        decimal Debit,
        decimal Credit,
        string Narration,
        Guid? StoreGroupId = null,
        Guid? StoreId = null);
    private sealed record StockAccountingStore(Guid Id, Guid StoreGroupId, string Name, string StoreCode);

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

    public async Task<StockOperationAccountingResult?> PostStockOperationAsync(
        StockOperationDocument document,
        StockOperationAccountingKind kind,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var plan = StockOperationAccountingCalculator.Create(kind, amount);
        var accountingAmount = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        if (accountingAmount == 0)
        {
            return null;
        }

        var sourceStoreId = document.FromStoreId ?? document.StoreId;
        var sourceStore = await db.Stores.AsNoTracking()
            .Where(store => store.Id == sourceStoreId && store.CompanyId == document.CompanyId)
            .Select(store => new StockAccountingStore(store.Id, store.StoreGroupId, store.Name, store.StoreCode))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The source store for stock accounting was not found.");

        var sourceInventory = await EnsureNamedLedgerAsync(
            document.CompanyId,
            InventoryLedgerName(sourceStore.StoreCode, sourceStore.Name),
            "Stock-in-Hand",
            LedgerCategory.Stock,
            LedgerType.StockItem,
            cancellationToken);

        Ledger? destinationInventory = null;
        StockAccountingStore? destinationStore = null;
        if (plan.DestinationInventoryDebit > 0)
        {
            if (!document.ToStoreId.HasValue)
            {
                throw new InvalidOperationException("A destination store is required for transfer accounting.");
            }

            destinationStore = await db.Stores.AsNoTracking()
                .Where(store => store.Id == document.ToStoreId.Value && store.CompanyId == document.CompanyId)
                .Select(store => new StockAccountingStore(store.Id, store.StoreGroupId, store.Name, store.StoreCode))
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The destination store for stock accounting was not found.");

            destinationInventory = await EnsureNamedLedgerAsync(
                document.CompanyId,
                InventoryLedgerName(destinationStore.StoreCode, destinationStore.Name),
                "Stock-in-Hand",
                LedgerCategory.Stock,
                LedgerType.StockItem,
                cancellationToken);
        }

        Ledger? stockGain = null;
        if (plan.GainCredit > 0)
        {
            stockGain = await EnsureNamedLedgerAsync(
                document.CompanyId,
                "Stock Adjustment Gain",
                "Indirect Income",
                LedgerCategory.IndirectIncome,
                LedgerType.Income,
                cancellationToken);
        }

        Ledger? stockLoss = null;
        if (plan.LossDebit > 0)
        {
            stockLoss = await EnsureNamedLedgerAsync(
                document.CompanyId,
                kind == StockOperationAccountingKind.WriteOff ? "Stock Write-off" : "Stock Shortage",
                "Indirect Expenses",
                LedgerCategory.IndirectExpenses,
                LedgerType.Expenses,
                cancellationToken);
        }

        var operationLabel = kind switch
        {
            StockOperationAccountingKind.Excess => "stock excess",
            StockOperationAccountingKind.Shortage => "stock shortage",
            StockOperationAccountingKind.WriteOff => "stock write-off",
            StockOperationAccountingKind.Transfer => "inter-store stock transfer",
            _ => "stock operation"
        };
        var lines = new List<JournalLineDraft>();
        if (plan.SourceInventoryDebit > 0)
        {
            lines.Add(new(sourceInventory.Id, null, plan.SourceInventoryDebit, 0, $"{operationLabel}: inventory increase"));
        }
        if (plan.SourceInventoryCredit > 0)
        {
            lines.Add(new(sourceInventory.Id, null, 0, plan.SourceInventoryCredit, $"{operationLabel}: inventory reduction"));
        }
        if (plan.DestinationInventoryDebit > 0 && destinationInventory is not null && destinationStore is not null)
        {
            lines.Add(new(
                destinationInventory.Id,
                null,
                plan.DestinationInventoryDebit,
                0,
                $"{operationLabel}: inventory received",
                destinationStore.StoreGroupId,
                destinationStore.Id));
        }
        if (plan.GainCredit > 0 && stockGain is not null)
        {
            lines.Add(new(stockGain.Id, null, 0, plan.GainCredit, $"{operationLabel}: gain recognized"));
        }
        if (plan.LossDebit > 0 && stockLoss is not null)
        {
            lines.Add(new(stockLoss.Id, null, plan.LossDebit, 0, $"{operationLabel}: loss recognized"));
        }

        var journal = await RepostSourceJournalAsync(
            "StockOperationDocument",
            document.Id,
            $"JE-{document.DocumentNumber}",
            document.OnDate,
            document.DocumentNumber,
            $"{operationLabel} posted from {document.DocumentNumber}. {document.Reason}".Trim(),
            document.CompanyId,
            sourceStore.StoreGroupId,
            sourceStore.Id,
            lines,
            cancellationToken);

        return new StockOperationAccountingResult(journal.Id, journal.EntryNumber, accountingAmount);
    }


    public async Task<GstAccountingBridgeSummary> GetGstAccountingBridgeSummaryAsync(
        Guid companyId,
        string returnPeriod,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken)
    {
        var outputRow = await BuildTaxLedgerRowAsync(companyId, "Output GST", periodStart, periodEndExclusive, creditPositive: true, "Output tax liability from posted sales/returns", cancellationToken);
        var inputRow = await BuildTaxLedgerRowAsync(companyId, "Input GST", periodStart, periodEndExclusive, creditPositive: false, "Input tax credit from posted purchases/returns", cancellationToken);
        var outputTax = Math.Max(0, outputRow.NetAmount);
        var inputTax = Math.Max(0, inputRow.NetAmount);
        var netPayable = Math.Max(0, outputTax - inputTax);
        var creditCarryForward = Math.Max(0, inputTax - outputTax);
        var reference = GstAccountingReference(returnPeriod);
        var existing = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.CompanyId == companyId
                && entry.SourceType == "GstReturnAccounting"
                && entry.ReferenceNumber == reference,
                cancellationToken);

        var status = existing is not null
            ? "Posted"
            : netPayable > 0
                ? "Payable"
                : creditCarryForward > 0
                    ? "Credit Carry Forward"
                    : "No GST Payable";

        return new GstAccountingBridgeSummary(
            companyId,
            returnPeriod,
            periodStart,
            periodEndExclusive,
            outputTax,
            inputTax,
            netPayable,
            creditCarryForward,
            0,
            existing is not null,
            existing?.Id,
            existing?.EntryNumber,
            status,
            [outputRow, inputRow]);
    }

    public async Task<GstAccountingPostResult> PostGstAccountingSettlementAsync(
        GstAccountingPostRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("Company is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ReturnPeriod))
        {
            throw new ArgumentException("GST return period is required.");
        }

        var outputTax = Math.Round(Math.Max(0, request.OutputTax), 2, MidpointRounding.AwayFromZero);
        var inputTax = Math.Round(Math.Max(0, request.InputTax), 2, MidpointRounding.AwayFromZero);
        var interestLateFee = Math.Round(Math.Max(0, request.InterestLateFee), 2, MidpointRounding.AwayFromZero);
        var netPayable = Math.Round(Math.Max(0, outputTax - inputTax), 2, MidpointRounding.AwayFromZero);
        var creditCarryForward = Math.Round(Math.Max(0, inputTax - outputTax), 2, MidpointRounding.AwayFromZero);

        if (outputTax == 0 && inputTax == 0 && interestLateFee == 0)
        {
            throw new InvalidOperationException("No GST tax/ITC/fee amount is available for accounting posting.");
        }

        var storeInfo = await ResolvePostingStoreAsync(request.CompanyId, request.StoreGroupId, request.StoreId, cancellationToken);
        var outputLedger = await EnsureNamedLedgerAsync(request.CompanyId, "Output GST", "Duties & Taxes", LedgerCategory.DutiesAndTaxes, LedgerType.DutyAndTax, cancellationToken);
        var inputLedger = await EnsureNamedLedgerAsync(request.CompanyId, "Input GST", "Duties & Taxes", LedgerCategory.DutiesAndTaxes, LedgerType.DutyAndTax, cancellationToken);
        var payableLedger = await EnsureNamedLedgerAsync(request.CompanyId, "GST Payable", "Duties & Taxes", LedgerCategory.DutiesAndTaxes, LedgerType.CurrentLiability, cancellationToken);
        var carryForwardLedger = await EnsureNamedLedgerAsync(request.CompanyId, "GST Credit Carry Forward", "Current Assets", LedgerCategory.CurrentAssets, LedgerType.CurrentAsset, cancellationToken);
        var interestLedger = await EnsureNamedLedgerAsync(request.CompanyId, "GST Interest & Late Fee", "Indirect Expenses", LedgerCategory.IndirectExpenses, LedgerType.Expenses, cancellationToken);

        var narration = string.IsNullOrWhiteSpace(request.Narration)
            ? $"GST accounting settlement for {request.ReturnPeriod}"
            : request.Narration.Trim();
        var lines = new List<JournalLineDraft>();

        if (outputTax > 0)
        {
            lines.Add(new(outputLedger.Id, null, outputTax, 0, $"Transfer output GST for {request.ReturnPeriod}"));
        }

        if (inputTax > 0)
        {
            lines.Add(new(inputLedger.Id, null, 0, inputTax, $"Set off input GST credit for {request.ReturnPeriod}"));
        }

        if (netPayable > 0)
        {
            lines.Add(new(payableLedger.Id, null, 0, netPayable, $"GST payable for {request.ReturnPeriod}"));
        }

        if (creditCarryForward > 0)
        {
            lines.Add(new(carryForwardLedger.Id, null, creditCarryForward, 0, $"GST credit carry forward for {request.ReturnPeriod}"));
        }

        if (interestLateFee > 0)
        {
            lines.Add(new(interestLedger.Id, null, interestLateFee, 0, $"GST interest/late fee for {request.ReturnPeriod}"));
            lines.Add(new(payableLedger.Id, null, 0, interestLateFee, $"GST interest/late fee payable for {request.ReturnPeriod}"));
        }

        var reference = GstAccountingReference(request.ReturnPeriod);
        var journal = await RepostSourceJournalAsync(
            "GstReturnAccounting",
            request.DraftId ?? DeterministicGuid($"{request.CompanyId:N}|{request.ReturnPeriod}"),
            $"GST-{request.ReturnPeriod}",
            request.OnDate ?? DateTime.Now,
            reference,
            narration,
            request.CompanyId,
            storeInfo.StoreGroupId,
            storeInfo.StoreId,
            lines,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new GstAccountingPostResult(
            journal.Id,
            journal.EntryNumber,
            reference,
            outputTax,
            inputTax,
            netPayable,
            creditCarryForward,
            interestLateFee,
            netPayable > 0
                ? $"GST payable of {netPayable:0.00} posted to GST Payable."
                : creditCarryForward > 0
                    ? $"GST credit carry forward of {creditCarryForward:0.00} posted."
                    : "GST output/input transfer posted with no net payable.");
    }

    public async Task<AccountingPostResult> SaveVoucherAsync(
        VoucherSaveRequest request,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await SaveVoucherCoreAsync(request, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public Task<AccountingPostResult> SaveVoucherInCurrentTransactionAsync(
        VoucherSaveRequest request,
        CancellationToken cancellationToken)
        => SaveVoucherCoreAsync(request, cancellationToken);

    private async Task<AccountingPostResult> SaveVoucherCoreAsync(
        VoucherSaveRequest request,
        CancellationToken cancellationToken)
    {
        ValidateVoucher(request);
        if (request.Id.HasValue
            && await db.CashVoucherConversions.AnyAsync(item => item.VoucherId == request.Id.Value, cancellationToken))
        {
            throw new InvalidOperationException("Converted vouchers are immutable. Create a corrective voucher instead.");
        }

        var employeeExists = await db.Employees.AnyAsync(item => item.Id == request.EmployeeId, cancellationToken);
        if (!employeeExists)
        {
            throw new InvalidOperationException("Select a valid employee in Issued by.");
        }

        var ledger = await db.Ledgers.FirstOrDefaultAsync(item => item.Id == request.LedgerId, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid ledger.");
        var party = await ResolveVoucherPartyAsync(request, ledger, cancellationToken);

        var bankAccount = await ResolveBankAccountAsync(request, cancellationToken);
        var cashOrBankLedger = await ResolveCashOrBankLedgerAsync(request, bankAccount, cancellationToken);

        var voucher = await ResolveVoucherAsync(request, cancellationToken);
        voucher.VoucherNumber = request.Id.HasValue
            ? request.VoucherNumber.Trim()
            : await documentNumbers.NextVoucherAsync(
                request.CompanyId,
                request.StoreGroupId,
                request.StoreId,
                request.VoucherType,
                request.OnDate,
                cancellationToken);
        voucher.OnDate = request.OnDate;
        voucher.VoucherType = request.VoucherType;
        voucher.PartyName = party?.Name ?? request.PartyName.Trim();
        voucher.Particulars = request.Particulars.Trim();
        voucher.Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        voucher.Remarks = request.Remarks?.Trim() ?? string.Empty;
        voucher.SlipNumber = request.PaymentMode == PaymentMode.Cash
            ? request.SlipNumber?.Trim()
            : "NA";
        voucher.PaymentMode = request.PaymentMode;
        voucher.PaymentDetails = request.PaymentDetails?.Trim();
        voucher.IsParty = ledger.IsParty || party is not null;
        voucher.PartyId = party?.Id;
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
            .Select(line => new BankStatementRow(
                line.Id,
                line.OnDate,
                line.Description,
                line.Reference,
                line.Debit,
                line.Credit,
                line.Balance,
                line.Reconciled,
                line.ReconciledAt,
                line.ReconciledBy,
                line.ReconciliationReference,
                line.ReconciliationRemarks,
                line.BankTransactionId))
            .ToListAsync(cancellationToken);
    }

    public async Task<BankReconciliationSummary> GetBankReconciliationAsync(
        Guid bankAccountId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var account = await db.BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == bankAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid bank account.");

        var query = db.BankStatementLines.AsNoTracking()
            .Where(line => line.BankAccountId == bankAccountId);
        if (from.HasValue)
        {
            query = query.Where(line => line.OnDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(line => line.OnDate <= to.Value);
        }

        var lines = await query
            .OrderBy(line => line.OnDate)
            .ThenBy(line => line.CreatedAt)
            .ThenBy(line => line.Id)
            .ToListAsync(cancellationToken);

        var rows = lines.Select(line => new BankStatementRow(
            line.Id,
            line.OnDate,
            line.Description,
            line.Reference,
            line.Debit,
            line.Credit,
            line.Balance,
            line.Reconciled,
            line.ReconciledAt,
            line.ReconciledBy,
            line.ReconciliationReference,
            line.ReconciliationRemarks,
            line.BankTransactionId)).ToList();

        return new BankReconciliationSummary(
            account.Id,
            $"{account.AccountHolderName} - {account.AccountNumber}".Trim(' ', '-'),
            lines.LastOrDefault()?.Balance ?? account.ClosingBalance,
            lines.Where(line => !line.Reconciled).Sum(line => line.Debit),
            lines.Where(line => !line.Reconciled).Sum(line => line.Credit),
            lines.Where(line => line.Reconciled).Sum(line => line.Debit),
            lines.Where(line => line.Reconciled).Sum(line => line.Credit),
            lines.Count(line => !line.Reconciled),
            lines.Count(line => line.Reconciled),
            rows);
    }

    public async Task<BankStatementRow> ReconcileBankStatementLineAsync(
        Guid statementLineId,
        BankStatementReconcileRequest request,
        string? reconciledBy,
        CancellationToken cancellationToken)
    {
        var line = await db.BankStatementLines.FirstOrDefaultAsync(item => item.Id == statementLineId, cancellationToken)
            ?? throw new InvalidOperationException("Bank statement line was not found.");

        if (request.BankTransactionId.HasValue && request.BankTransactionId.Value != Guid.Empty)
        {
            var transaction = await db.BankTransactions.FirstOrDefaultAsync(
                item => item.Id == request.BankTransactionId.Value && item.BankAccountId == line.BankAccountId,
                cancellationToken)
                ?? throw new InvalidOperationException("Select a valid transaction from the same bank account.");

            line.BankTransactionId = transaction.Id;
            transaction.Reconciled = true;
            transaction.ReconciledAt = request.ReconciledAt ?? DateTime.Now;
            transaction.ReconciledBy = reconciledBy;
            transaction.ReconciliationReference = CleanText(request.ReconciliationReference) ?? line.Reference;
            transaction.ReconciliationRemarks = CleanText(request.Remarks);
        }

        line.Reconciled = true;
        line.ReconciledAt = request.ReconciledAt ?? DateTime.Now;
        line.ReconciledBy = reconciledBy;
        line.ReconciliationReference = CleanText(request.ReconciliationReference) ?? line.Reference;
        line.ReconciliationRemarks = CleanText(request.Remarks);
        await db.SaveChangesAsync(cancellationToken);

        return new BankStatementRow(
            line.Id,
            line.OnDate,
            line.Description,
            line.Reference,
            line.Debit,
            line.Credit,
            line.Balance,
            line.Reconciled,
            line.ReconciledAt,
            line.ReconciledBy,
            line.ReconciliationReference,
            line.ReconciliationRemarks,
            line.BankTransactionId);
    }

    public async Task<BankStatementRow> UnreconcileBankStatementLineAsync(
        Guid statementLineId,
        string? remarks,
        CancellationToken cancellationToken)
    {
        var line = await db.BankStatementLines.FirstOrDefaultAsync(item => item.Id == statementLineId, cancellationToken)
            ?? throw new InvalidOperationException("Bank statement line was not found.");

        if (line.BankTransactionId.HasValue)
        {
            var transaction = await db.BankTransactions.FirstOrDefaultAsync(item => item.Id == line.BankTransactionId.Value, cancellationToken);
            if (transaction is not null)
            {
                transaction.Reconciled = false;
                transaction.ReconciledAt = null;
                transaction.ReconciledBy = null;
                transaction.ReconciliationReference = null;
                transaction.ReconciliationRemarks = CleanText(remarks);
            }
        }

        line.Reconciled = false;
        line.ReconciledAt = null;
        line.ReconciledBy = null;
        line.ReconciliationReference = null;
        line.ReconciliationRemarks = CleanText(remarks);
        await db.SaveChangesAsync(cancellationToken);

        return new BankStatementRow(
            line.Id,
            line.OnDate,
            line.Description,
            line.Reference,
            line.Debit,
            line.Credit,
            line.Balance,
            line.Reconciled,
            line.ReconciledAt,
            line.ReconciledBy,
            line.ReconciliationReference,
            line.ReconciliationRemarks,
            line.BankTransactionId);
    }

    public async Task<ChequeLog> UpdateChequeLifecycleAsync(
        Guid chequeLogId,
        ChequeLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        var cheque = await db.ChequeLogs.FirstOrDefaultAsync(item => item.Id == chequeLogId, cancellationToken)
            ?? throw new InvalidOperationException("Cheque log was not found.");

        var status = NormalizeChequeStatus(request.Status);
        var actionDate = request.ActionDate ?? DateTime.Now;
        cheque.Status = status;
        cheque.LifecycleRemarks = CleanText(request.Remarks);
        if (request.BankTransactionId.HasValue && request.BankTransactionId.Value != Guid.Empty)
        {
            var transaction = await db.BankTransactions.FirstOrDefaultAsync(
                item => item.Id == request.BankTransactionId.Value && item.BankAccountId == cheque.BankAccountId,
                cancellationToken)
                ?? throw new InvalidOperationException("Select a valid transaction from the same bank account.");
            cheque.BankTransactionId = transaction.Id;
        }

        switch (status)
        {
            case "Issued":
                cheque.CancelledAt = null;
                cheque.BouncedAt = null;
                break;
            case "Deposited":
                cheque.DepositedAt = actionDate;
                cheque.CancelledAt = null;
                break;
            case "Cleared":
                cheque.ClearedAt = actionDate;
                cheque.BouncedAt = null;
                cheque.CancelledAt = null;
                break;
            case "Bounced":
                cheque.BouncedAt = actionDate;
                cheque.ClearedAt = null;
                break;
            case "Cancelled":
                cheque.CancelledAt = actionDate;
                break;
        }

        await db.SaveChangesAsync(cancellationToken);
        return cheque;
    }

    public async Task<List<LedgerStatementRow>> GetLedgerStatementAsync(
        Guid ledgerId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var ledger = await db.Ledgers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ledgerId, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid ledger.");

        var openingBalance = ledger.OpeningBalance;
        if (from.HasValue)
        {
            openingBalance += await db.JournalLines.AsNoTracking()
                .Where(line => line.LedgerId == ledgerId
                    && line.JournalEntry != null
                    && line.JournalEntry.OnDate < from.Value)
                .SumAsync(line => line.Debit - line.Credit, cancellationToken);
        }

        var query = db.JournalLines.AsNoTracking()
            .Include(line => line.JournalEntry)
            .Where(line => line.LedgerId == ledgerId);

        if (from.HasValue)
        {
            query = query.Where(line => line.JournalEntry != null && line.JournalEntry.OnDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(line => line.JournalEntry != null && line.JournalEntry.OnDate <= to.Value);
        }

        var lines = await query
            .OrderBy(line => line.JournalEntry!.OnDate)
            .ThenBy(line => line.JournalEntry!.CreatedAt)
            .ThenBy(line => line.Id)
            .ToListAsync(cancellationToken);

        var balance = openingBalance;
        var rows = new List<LedgerStatementRow>
        {
            new(
                Guid.Empty,
                ledger.Id,
                from ?? ledger.OpeningDate,
                "OPENING",
                "Opening",
                null,
                "Opening balance",
                openingBalance > 0 ? openingBalance : 0,
                openingBalance < 0 ? Math.Abs(openingBalance) : 0,
                Math.Abs(openingBalance),
                openingBalance >= 0 ? "Dr" : "Cr")
        };

        foreach (var line in lines)
        {
            balance += line.Debit - line.Credit;
            rows.Add(new LedgerStatementRow(
                line.Id,
                ledger.Id,
                line.JournalEntry?.OnDate ?? line.CreatedAt,
                line.JournalEntry?.EntryNumber ?? string.Empty,
                line.JournalEntry?.SourceType ?? string.Empty,
                line.JournalEntry?.ReferenceNumber,
                line.Narration ?? line.JournalEntry?.Narration ?? string.Empty,
                line.Debit,
                line.Credit,
                Math.Abs(balance),
                balance >= 0 ? "Dr" : "Cr"));
        }

        return rows;
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

        if (request.StoreGroupId == Guid.Empty || request.StoreId == Guid.Empty)
        {
            throw new ArgumentException("Store is required.");
        }

        if (request.LedgerId == Guid.Empty)
        {
            throw new ArgumentException("Contra ledger is required.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        var bankAccount = await db.BankAccounts.FirstOrDefaultAsync(item => item.Id == request.BankAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid bank account.");
        var bankLedger = await db.Ledgers.FirstOrDefaultAsync(item => item.Id == bankAccount.LedgerId, cancellationToken)
            ?? throw new InvalidOperationException("The selected bank account is not linked to a ledger.");
        var contraLedger = await db.Ledgers.FirstOrDefaultAsync(item => item.Id == request.LedgerId, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid contra ledger.");
        if (contraLedger.Id == bankLedger.Id)
        {
            throw new InvalidOperationException("Contra ledger cannot be the selected bank account ledger.");
        }

        var party = await ResolveBankTransactionPartyAsync(request.PartyId, contraLedger, request.CompanyId, cancellationToken);

        var transaction = request.Id.HasValue
            ? await db.BankTransactions.FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken)
            : null;
        var previousBankAccountId = transaction?.BankAccountId;

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
        await UpsertManualChequeLogAsync(transaction, bankAccount, cancellationToken);
        var lineNarration = transaction.Narration ?? string.Empty;
        var lines = request.TransactionType == TransactionType.Deposit
            ? new List<JournalLineDraft>
            {
                new(bankLedger.Id, null, transaction.Amount, 0, lineNarration),
                new(contraLedger.Id, party?.Id, 0, transaction.Amount, lineNarration)
            }
            : [
                new(contraLedger.Id, party?.Id, transaction.Amount, 0, lineNarration),
                new(bankLedger.Id, null, 0, transaction.Amount, lineNarration)
            ];
        await RepostSourceJournalAsync(
            "BankTransaction",
            transaction.Id,
            $"BT-{transaction.Reference}",
            transaction.OnDate,
            transaction.Reference,
            transaction.Narration ?? $"Bank transaction {transaction.Reference}",
            request.CompanyId,
            request.StoreGroupId,
            request.StoreId,
            lines,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await RecalculateBankStatementBalancesAsync(bankAccount.Id, cancellationToken);
        if (previousBankAccountId.HasValue && previousBankAccountId.Value != bankAccount.Id)
        {
            await RecalculateBankStatementBalancesAsync(previousBankAccountId.Value, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    private async Task<Party?> ResolveBankTransactionPartyAsync(
        Guid? partyId,
        Ledger contraLedger,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (partyId.HasValue && partyId.Value != Guid.Empty)
        {
            var party = await db.Parties.FirstOrDefaultAsync(item => item.Id == partyId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Select a valid party.");

            if (party.LedgerId == Guid.Empty)
            {
                party.LedgerId = (await EnsurePartyLedgerAsync(party, cancellationToken)).Id;
            }

            return party;
        }

        if (!contraLedger.IsParty)
        {
            return null;
        }

        var linkedParty = await db.Parties.FirstOrDefaultAsync(
            item => item.CompanyId == companyId && item.LedgerId == contraLedger.Id,
            cancellationToken);

        if (linkedParty is not null)
        {
            return linkedParty;
        }

        linkedParty = new Party
        {
            CompanyId = companyId,
            Name = contraLedger.Name,
            Category = PartyType.Others,
            LedgerId = contraLedger.Id
        };
        db.Parties.Add(linkedParty);
        return linkedParty;
    }


    public async Task<LedgerSyncSummary> ValidateLedgerSynchronizationAsync(
        Guid? companyId,
        bool repair,
        CancellationToken cancellationToken)
    {
        var issues = new List<LedgerSyncIssue>();
        var repairedCount = 0;

        var partyQuery = db.Parties.AsQueryable();
        var bankQuery = db.BankAccounts.AsQueryable();
        if (companyId.HasValue && companyId.Value != Guid.Empty)
        {
            partyQuery = partyQuery.Where(item => item.CompanyId == companyId.Value);
            bankQuery = bankQuery.Where(item => item.CompanyId == companyId.Value);
        }

        var parties = await partyQuery.OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var bankAccounts = await bankQuery.OrderBy(item => item.AccountHolderName).ThenBy(item => item.AccountNumber).ToListAsync(cancellationToken);

        foreach (var party in parties)
        {
            var issue = await GetPartyLedgerSyncIssueAsync(party, cancellationToken);
            if (issue is null)
            {
                continue;
            }

            if (repair)
            {
                party.LedgerId = (await EnsurePartyLedgerAsync(party, cancellationToken)).Id;
                repairedCount++;
                issues.Add(issue with { Severity = "Repaired", FixAction = "Party ledger was created or relinked." });
            }
            else
            {
                issues.Add(issue);
            }
        }

        foreach (var account in bankAccounts)
        {
            var issue = await GetBankAccountLedgerSyncIssueAsync(account, cancellationToken);
            if (issue is null)
            {
                continue;
            }

            if (repair)
            {
                account.LedgerId = (await EnsureBankAccountLedgerAsync(account, cancellationToken)).Id;
                repairedCount++;
                issues.Add(issue with { Severity = "Repaired", FixAction = "Bank-account ledger was created or relinked." });
            }
            else
            {
                issues.Add(issue);
            }
        }

        if (repair && repairedCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new LedgerSyncSummary(
            companyId,
            parties.Count,
            bankAccounts.Count,
            issues.Count,
            repairedCount,
            issues);
    }

    public async Task<Party> SavePartyAsync(
        PartySaveRequest request,
        Guid? partyId,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("Company is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Party name is required.");
        }

        var party = !partyId.HasValue || partyId.Value == Guid.Empty
            ? null
            : await db.Parties.FirstOrDefaultAsync(item => item.Id == partyId.Value, cancellationToken);

        if (partyId.HasValue && party is null)
        {
            throw new InvalidOperationException("Party was not found.");
        }

        party ??= new Party
        {
            CompanyId = request.CompanyId,
            Name = request.Name.Trim()
        };

        party.CompanyId = request.CompanyId;
        party.Name = request.Name.Trim();
        party.Address = request.Address?.Trim();
        party.EmailId = request.EmailId?.Trim();
        party.Phone = request.Phone?.Trim();
        party.GSTIN = request.GSTIN?.Trim();
        party.PAN = request.PAN?.Trim();
        party.Category = request.Category;
        party.LedgerId = (await EnsurePartyLedgerAsync(party, cancellationToken)).Id;

        if (db.Entry(party).State == EntityState.Detached)
        {
            db.Parties.Add(party);
        }

        await db.SaveChangesAsync(cancellationToken);
        return party;
    }

    public async Task<BankAccount> SaveBankAccountAsync(
        BankAccountSaveRequest request,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("Company is required.");
        }

        if (request.BankId == Guid.Empty)
        {
            throw new ArgumentException("Bank is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            throw new ArgumentException("Account number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AccountHolderName))
        {
            throw new ArgumentException("Account holder name is required.");
        }

        var account = !accountId.HasValue || accountId.Value == Guid.Empty
            ? null
            : await db.BankAccounts.FirstOrDefaultAsync(item => item.Id == accountId.Value, cancellationToken);

        if (accountId.HasValue && account is null)
        {
            throw new InvalidOperationException("Bank account was not found.");
        }

        account ??= new BankAccount
        {
            CompanyId = request.CompanyId
        };

        account.CompanyId = request.CompanyId;
        account.AccountNumber = request.AccountNumber.Trim();
        account.AccountHolderName = request.AccountHolderName.Trim();
        account.BankId = request.BankId;
        account.AccountType = request.AccountType;
        account.Branch = request.Branch?.Trim();
        account.IFSCode = request.IFSCode?.Trim();
        account.OpeningDate = request.OpeningDate.Date;
        account.Active = request.Active;
        account.ClosingDate = request.ClosingDate?.Date;
        account.OpeningBalance = request.OpeningBalance;
        account.ClosingBalance = request.ClosingBalance;
        account.LedgerId = (await EnsureBankAccountLedgerAsync(account, cancellationToken)).Id;

        if (db.Entry(account).State == EntityState.Detached)
        {
            db.BankAccounts.Add(account);
        }

        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task<Party> EnsureCustomerPartyAsync(Customer customer, CancellationToken cancellationToken)
    {
        var party = customer.PartyId.HasValue
            ? await db.Parties.FirstOrDefaultAsync(item => item.Id == customer.PartyId.Value, cancellationToken)
            : null;

        party ??= await db.Parties.FirstOrDefaultAsync(
            item => item.CompanyId == customer.CompanyId && item.Name == customer.Name && item.Category == PartyType.Customer,
            cancellationToken);

        party ??= new Party
        {
            CompanyId = customer.CompanyId,
            Name = customer.Name,
            Category = PartyType.Customer
        };

        party.CompanyId = customer.CompanyId;
        party.Name = customer.Name.Trim();
        party.Category = PartyType.Customer;
        party.Address = customer.Address;
        party.EmailId = customer.Email;
        party.Phone = customer.MobileNumber;
        party.GSTIN = customer.GSTIN;
        party.LedgerId = (await EnsurePartyLedgerAsync(party, cancellationToken)).Id;

        if (db.Entry(party).State == EntityState.Detached)
        {
            db.Parties.Add(party);
        }

        customer.PartyId = party.Id;
        return party;
    }

    public async Task<Party> EnsureVendorPartyAsync(Vendor vendor, CancellationToken cancellationToken)
    {
        var party = vendor.PartyId.HasValue
            ? await db.Parties.FirstOrDefaultAsync(item => item.Id == vendor.PartyId.Value, cancellationToken)
            : null;

        party ??= await db.Parties.FirstOrDefaultAsync(
            item => item.CompanyId == vendor.CompanyId && item.Name == vendor.Name && item.Category == PartyType.Vendor,
            cancellationToken);

        party ??= new Party
        {
            CompanyId = vendor.CompanyId,
            Name = vendor.Name,
            Category = PartyType.Vendor
        };

        party.CompanyId = vendor.CompanyId;
        party.Name = vendor.Name.Trim();
        party.Category = PartyType.Vendor;
        party.Address = vendor.Address;
        party.EmailId = vendor.Email;
        party.Phone = vendor.MobileNumber;
        party.GSTIN = vendor.GSTIN;
        party.PAN = vendor.Pan;
        party.LedgerId = (await EnsurePartyLedgerAsync(party, cancellationToken)).Id;

        if (db.Entry(party).State == EntityState.Detached)
        {
            db.Parties.Add(party);
        }

        vendor.PartyId = party.Id;
        return party;
    }

    public async Task PostSalesInvoiceAsync(
        Invoice invoice,
        Customer customer,
        Guid storeGroupId,
        IReadOnlyList<SalesInvoicePaymentPosting>? payments,
        CancellationToken cancellationToken)
    {
        var party = await EnsureCustomerPartyAsync(customer, cancellationToken);
        var customerLedgerId = party.LedgerId;
        var salesLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Sales", "Sales Accounts", LedgerCategory.SalesAccounts, LedgerType.Sale, cancellationToken);
        var outputGstLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Output GST", "Duties & Taxes", LedgerCategory.DutiesAndTaxes, LedgerType.DutyAndTax, cancellationToken);
        var roundOffLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Round Off", "Indirect Income", LedgerCategory.IndirectIncome, LedgerType.Income, cancellationToken);
        var paymentPostings = NormalizeSalesInvoicePaymentPostings(invoice, payments);

        var lines = new List<JournalLineDraft>
        {
            new(customerLedgerId, party.Id, invoice.BillAmount, 0, $"Sales invoice {invoice.InvoiceNumber}"),
            new(salesLedger.Id, null, 0, invoice.NetAmount, $"Sales invoice {invoice.InvoiceNumber}")
        };

        if (invoice.TaxAmount > 0)
        {
            lines.Add(new(outputGstLedger.Id, null, 0, invoice.TaxAmount, $"Sales tax {invoice.InvoiceNumber}"));
        }

        AddRoundOff(lines, roundOffLedger.Id, invoice.RoundOff, isSale: true, invoice.InvoiceNumber);

        for (var index = 0; index < paymentPostings.Count; index++)
        {
            var payment = paymentPostings[index];
            var amount = Math.Round(payment.Amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                continue;
            }

            var rowNumber = index + 1;
            var narration = BuildSalesInvoicePaymentNarration(invoice, payment, rowNumber);
            var sourceReference = BuildSalesInvoicePaymentSourceReference(invoice, rowNumber);
            var settlementLedger = await ResolveSalesInvoiceSettlementLedgerAsync(invoice.CompanyId, payment, cancellationToken);

            lines.Add(new(settlementLedger.Id, null, amount, 0, narration));
            lines.Add(new(customerLedgerId, party.Id, 0, amount, narration));

            if (BankPaymentModes.Contains(payment.PaymentMode))
            {
                await UpsertInvoiceBankTransactionAsync(
                    invoice.CompanyId,
                    payment.PaymentMode,
                    payment.BankAccountId,
                    TransactionType.Deposit,
                    invoice.OnDate,
                    sourceReference,
                    narration,
                    amount,
                    customer.Name,
                    cancellationToken,
                    FirstNonEmpty(payment.ReferenceNumber, payment.GatewayReference));
            }
        }

        await RepostSourceJournalAsync(
            "SalesInvoice",
            invoice.Id,
            $"SI-{invoice.InvoiceNumber}",
            invoice.OnDate,
            invoice.InvoiceNumber,
            $"Sales invoice {invoice.InvoiceNumber}",
            invoice.CompanyId,
            storeGroupId,
            invoice.StoreId,
            lines,
            cancellationToken);
    }

    public async Task PostSalesInvoiceCancellationAsync(
        Invoice invoice,
        Customer? customer,
        Guid storeGroupId,
        decimal originalPaidAmount,
        PaymentMode? originalPaymentMode,
        Guid? legacyBankAccountId,
        IReadOnlyList<SalesInvoicePaymentPosting>? originalPayments,
        CancellationToken cancellationToken)
    {
        if (customer is null)
        {
            return;
        }

        var party = await EnsureCustomerPartyAsync(customer, cancellationToken);
        var customerLedgerId = party.LedgerId;
        var salesLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Sales", "Sales Accounts", LedgerCategory.SalesAccounts, LedgerType.Sale, cancellationToken);
        var outputGstLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Output GST", "Duties & Taxes", LedgerCategory.DutiesAndTaxes, LedgerType.DutyAndTax, cancellationToken);
        var roundOffLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Round Off", "Indirect Income", LedgerCategory.IndirectIncome, LedgerType.Income, cancellationToken);
        var paymentPostings = NormalizeSalesInvoiceCancellationPaymentPostings(originalPaidAmount, originalPaymentMode, legacyBankAccountId, originalPayments);

        var lines = new List<JournalLineDraft>
        {
            new(customerLedgerId, party.Id, 0, invoice.BillAmount, $"Cancel sales invoice {invoice.InvoiceNumber}"),
            new(salesLedger.Id, null, invoice.NetAmount, 0, $"Cancel sales invoice {invoice.InvoiceNumber}")
        };

        if (invoice.TaxAmount > 0)
        {
            lines.Add(new(outputGstLedger.Id, null, invoice.TaxAmount, 0, $"Cancel sales tax {invoice.InvoiceNumber}"));
        }

        AddRoundOff(lines, roundOffLedger.Id, invoice.RoundOff * -1, isSale: true, invoice.InvoiceNumber);

        for (var index = 0; index < paymentPostings.Count; index++)
        {
            var payment = paymentPostings[index];
            var amount = Math.Round(payment.Amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                continue;
            }

            var rowNumber = index + 1;
            var narration = $"Reverse {BuildSalesInvoicePaymentNarration(invoice, payment, rowNumber)}";
            var sourceReference = $"SIC-{invoice.InvoiceNumber}-PAY-{rowNumber:00}";
            var settlementLedger = await ResolveSalesInvoiceSettlementLedgerAsync(invoice.CompanyId, payment, cancellationToken);

            lines.Add(new(settlementLedger.Id, null, 0, amount, narration));
            lines.Add(new(customerLedgerId, party.Id, amount, 0, narration));

            if (BankPaymentModes.Contains(payment.PaymentMode))
            {
                await UpsertInvoiceBankTransactionAsync(
                    invoice.CompanyId,
                    payment.PaymentMode,
                    payment.BankAccountId,
                    TransactionType.Withdraw,
                    DateTime.Now,
                    sourceReference,
                    narration,
                    amount,
                    customer.Name,
                    cancellationToken,
                    FirstNonEmpty(payment.ReferenceNumber, payment.GatewayReference));
            }
        }

        await RepostSourceJournalAsync(
            "SalesInvoiceCancellation",
            invoice.Id,
            $"SIC-{invoice.InvoiceNumber}",
            DateTime.Now,
            invoice.InvoiceNumber,
            $"Cancel sales invoice {invoice.InvoiceNumber}",
            invoice.CompanyId,
            storeGroupId,
            invoice.StoreId,
            lines,
            cancellationToken);
    }

    public async Task PostPurchaseInvoiceAsync(
        PurchaseInvoice invoice,
        Vendor vendor,
        decimal paidAmount,
        Guid storeGroupId,
        Guid storeId,
        Guid? bankAccountId,
        CancellationToken cancellationToken)
    {
        var party = await EnsureVendorPartyAsync(vendor, cancellationToken);
        var vendorLedgerId = party.LedgerId;
        var purchaseLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Purchases", "Purchase Accounts", LedgerCategory.PurchaseAccounts, LedgerType.Purcahase, cancellationToken);
        var inputGstLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Input GST", "Duties & Taxes", LedgerCategory.DutiesAndTaxes, LedgerType.DutyAndTax, cancellationToken);
        var freightLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Transport & Freight Charges", "Direct Expenses", LedgerCategory.DirectExpenses, LedgerType.Expenses, cancellationToken);
        var roundOffLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Round Off", "Indirect Income", LedgerCategory.IndirectIncome, LedgerType.Income, cancellationToken);

        var lines = new List<JournalLineDraft>
        {
            new(purchaseLedger.Id, null, invoice.BasePrice, 0, $"Purchase invoice {invoice.InvoiceNumber}")
        };

        if (invoice.TaxAmount > 0)
        {
            lines.Add(new(inputGstLedger.Id, null, invoice.TaxAmount, 0, $"Purchase tax {invoice.InvoiceNumber}"));
        }

        if (invoice.FrightAmount > 0)
        {
            lines.Add(new(freightLedger.Id, null, invoice.FrightAmount, 0, $"Purchase freight {invoice.InvoiceNumber}"));
        }

        AddRoundOff(lines, roundOffLedger.Id, invoice.RoundOff, isSale: false, invoice.InvoiceNumber);
        lines.Add(new(vendorLedgerId, party.Id, 0, invoice.BillAmount, $"Purchase invoice {invoice.InvoiceNumber}"));

        if (invoice.PaymentMode.HasValue && paidAmount > 0)
        {
            paidAmount = Math.Min(paidAmount, invoice.BillAmount);
            var settlementLedger = await ResolveSettlementLedgerAsync(invoice.CompanyId, invoice.PaymentMode.Value, bankAccountId, cancellationToken);
            lines.Add(new(vendorLedgerId, party.Id, paidAmount, 0, $"Purchase payment {invoice.InvoiceNumber}"));
            lines.Add(new(settlementLedger.Id, null, 0, paidAmount, $"Purchase payment {invoice.InvoiceNumber}"));
            await UpsertInvoiceBankTransactionAsync(
                invoice.CompanyId,
                invoice.PaymentMode.Value,
                bankAccountId,
                TransactionType.Withdraw,
                invoice.OnDate,
                $"PI-{invoice.InvoiceNumber}",
                $"Purchase payment {invoice.InvoiceNumber}",
                paidAmount,
                vendor.Name,
                cancellationToken);
        }

        await RepostSourceJournalAsync(
            "PurchaseInvoice",
            invoice.Id,
            $"PI-{invoice.InvoiceNumber}",
            invoice.OnDate,
            invoice.InvoiceNumber,
            $"Purchase invoice {invoice.InvoiceNumber}",
            invoice.CompanyId,
            storeGroupId,
            storeId,
            lines,
            cancellationToken);
    }

    public async Task<JournalEntry?> PostPurchaseInvoiceCancellationAsync(
        PurchaseInvoice invoice,
        Vendor? vendor,
        Guid storeGroupId,
        Guid storeId,
        decimal originalPaidAmount,
        PaymentMode? originalPaymentMode,
        Guid? bankAccountId,
        IReadOnlyList<PurchaseReturnTaxPosting> taxReversals,
        CancellationToken cancellationToken)
    {
        if (vendor is null)
        {
            return null;
        }

        var party = await EnsureVendorPartyAsync(vendor, cancellationToken);
        var vendorLedgerId = party.LedgerId;
        var purchaseLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Purchases", "Purchase Accounts", LedgerCategory.PurchaseAccounts, LedgerType.Purcahase, cancellationToken);
        var inputGstLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Input GST", "Duties & Taxes", LedgerCategory.DutiesAndTaxes, LedgerType.DutyAndTax, cancellationToken);
        var freightLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Transport & Freight Charges", "Direct Expenses", LedgerCategory.DirectExpenses, LedgerType.Expenses, cancellationToken);
        var roundOffLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Round Off", "Indirect Income", LedgerCategory.IndirectIncome, LedgerType.Income, cancellationToken);

        var lines = new List<JournalLineDraft>
        {
            new(vendorLedgerId, party.Id, invoice.BillAmount, 0, $"Cancel purchase invoice {invoice.InvoiceNumber}"),
            new(purchaseLedger.Id, null, 0, invoice.BasePrice, $"Cancel purchase invoice {invoice.InvoiceNumber}")
        };

        foreach (var reversal in taxReversals.Where(item => item.TaxAmount > 0))
        {
            var hsn = string.IsNullOrWhiteSpace(reversal.HsnCode) ? string.Empty : $" | HSN {reversal.HsnCode}";
            lines.Add(new(
                inputGstLedger.Id,
                null,
                0,
                reversal.TaxAmount,
                $"ITC reversal {reversal.ProductName}{hsn} | Cancel {invoice.InvoiceNumber}"));
        }

        if (invoice.FrightAmount > 0)
        {
            lines.Add(new(freightLedger.Id, null, 0, invoice.FrightAmount, $"Cancel purchase freight {invoice.InvoiceNumber}"));
        }

        AddRoundOff(lines, roundOffLedger.Id, invoice.RoundOff * -1, isSale: false, invoice.InvoiceNumber);

        if (originalPaidAmount > 0 && originalPaymentMode.HasValue)
        {
            var settlementLedger = await ResolveSettlementLedgerAsync(invoice.CompanyId, originalPaymentMode.Value, bankAccountId, cancellationToken);
            lines.Add(new(settlementLedger.Id, null, originalPaidAmount, 0, $"Reverse purchase payment {invoice.InvoiceNumber}"));
            lines.Add(new(vendorLedgerId, party.Id, 0, originalPaidAmount, $"Reverse purchase payment {invoice.InvoiceNumber}"));
            await UpsertInvoiceBankTransactionAsync(
                invoice.CompanyId,
                originalPaymentMode.Value,
                bankAccountId,
                TransactionType.Deposit,
                DateTime.Now,
                $"PIC-{invoice.InvoiceNumber}",
                $"Reverse purchase payment {invoice.InvoiceNumber}",
                originalPaidAmount,
                vendor.Name,
                cancellationToken);
        }

        return await RepostSourceJournalAsync(
            "PurchaseInvoiceCancellation",
            invoice.Id,
            $"PIC-{invoice.InvoiceNumber}",
            DateTime.Now,
            invoice.InvoiceNumber,
            $"Cancel purchase invoice {invoice.InvoiceNumber}",
            invoice.CompanyId,
            storeGroupId,
            storeId,
            lines,
            cancellationToken);
    }


    public async Task PostPurchaseReturnAsync(
        PurchaseReturn purchaseReturn,
        PurchaseInvoice invoice,
        Vendor vendor,
        string debitNoteNumber,
        Guid storeGroupId,
        Guid storeId,
        decimal taxableAmount,
        decimal returnAmount,
        IReadOnlyList<PurchaseReturnTaxPosting> taxReversals,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (returnAmount <= 0)
        {
            throw new ArgumentException("Purchase return amount must be greater than zero.");
        }

        var party = await EnsureVendorPartyAsync(vendor, cancellationToken);
        var vendorLedgerId = party.LedgerId;
        var purchaseLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Purchases", "Purchase Accounts", LedgerCategory.PurchaseAccounts, LedgerType.Purcahase, cancellationToken);
        var inputGstLedger = await EnsureNamedLedgerAsync(invoice.CompanyId, "Input GST", "Duties & Taxes", LedgerCategory.DutiesAndTaxes, LedgerType.DutyAndTax, cancellationToken);
        var narration = string.IsNullOrWhiteSpace(reason)
            ? $"Purchase return {invoice.InvoiceNumber}"
            : reason.Trim();

        var lines = new List<JournalLineDraft>
        {
            new(vendorLedgerId, party.Id, returnAmount, 0, $"Debit note {debitNoteNumber} against {invoice.InvoiceNumber}")
        };

        if (taxableAmount > 0)
        {
            lines.Add(new(purchaseLedger.Id, null, 0, taxableAmount, $"Purchase return taxable {invoice.InvoiceNumber}"));
        }

        foreach (var reversal in taxReversals.Where(item => item.TaxAmount > 0))
        {
            var hsn = string.IsNullOrWhiteSpace(reversal.HsnCode) ? string.Empty : $" | HSN {reversal.HsnCode}";
            lines.Add(new(
                inputGstLedger.Id,
                null,
                0,
                reversal.TaxAmount,
                $"ITC reversal {reversal.ProductName}{hsn} | {purchaseReturn.ReturnNumber}"));
        }

        var journal = await RepostSourceJournalAsync(
            "PurchaseReturn",
            purchaseReturn.Id,
            $"PR-{purchaseReturn.ReturnNumber}",
            purchaseReturn.OnDate,
            debitNoteNumber,
            narration,
            invoice.CompanyId,
            storeGroupId,
            storeId,
            lines,
            cancellationToken);

        purchaseReturn.JournalEntryId = journal.Id;
        foreach (var reversal in db.PurchaseReturnItcReversals.Local.Where(item =>
                     item.PurchaseReturnId == purchaseReturn.Id &&
                     taxReversals.Any(posting => posting.ReversalId == item.Id)))
        {
            reversal.JournalEntryId = journal.Id;
        }
    }



    public async Task PostVendorPaymentVoucherAsync(
        Voucher voucher,
        Vendor vendor,
        Guid storeGroupId,
        Guid storeId,
        Guid? bankAccountId,
        CancellationToken cancellationToken)
    {
        if (voucher.Amount <= 0)
        {
            throw new ArgumentException("Vendor payment amount must be greater than zero.");
        }

        var party = await EnsureVendorPartyAsync(vendor, cancellationToken);
        var vendorLedgerId = party.LedgerId;
        var settlementLedger = await ResolveSettlementLedgerAsync(voucher.CompanyId, voucher.PaymentMode, bankAccountId, cancellationToken);
        voucher.PartyId = party.Id;
        voucher.LedgerId = vendorLedgerId;
        voucher.IsParty = true;
        voucher.StoreGroupId = storeGroupId;
        voucher.StoreId = storeId;

        var lines = new List<JournalLineDraft>
        {
            new(vendorLedgerId, party.Id, voucher.Amount, 0, voucher.Particulars),
            new(settlementLedger.Id, null, 0, voucher.Amount, voucher.Particulars)
        };

        await UpsertInvoiceBankTransactionAsync(
            voucher.CompanyId,
            voucher.PaymentMode,
            bankAccountId,
            TransactionType.Withdraw,
            voucher.OnDate,
            voucher.VoucherNumber,
            voucher.Particulars,
            voucher.Amount,
            vendor.Name,
            cancellationToken);

        await RepostSourceJournalAsync(
            "VendorPaymentVoucher",
            voucher.Id,
            voucher.VoucherNumber,
            voucher.OnDate,
            voucher.VoucherNumber,
            voucher.Particulars,
            voucher.CompanyId,
            storeGroupId,
            storeId,
            lines,
            cancellationToken);
    }

    public async Task<VendorRefundPostingResult> PostVendorRefundSettlementAsync(
        VendorSettlement settlement,
        Voucher voucher,
        Vendor vendor,
        CancellationToken cancellationToken)
    {
        if (settlement.RefundAmount <= 0)
        {
            throw new ArgumentException("Vendor refund amount must be greater than zero.");
        }

        if (!settlement.PaymentMode.HasValue)
        {
            throw new ArgumentException("Select a payment mode for the vendor refund.");
        }

        var party = await EnsureVendorPartyAsync(vendor, cancellationToken);
        var vendorLedgerId = party.LedgerId;
        var receiptLedger = await ResolveSettlementLedgerAsync(
            settlement.CompanyId,
            settlement.PaymentMode.Value,
            settlement.BankAccountId,
            cancellationToken);

        voucher.PartyId = party.Id;
        voucher.LedgerId = vendorLedgerId;
        voucher.IsParty = true;
        voucher.StoreGroupId = settlement.StoreGroupId;
        voucher.StoreId = settlement.StoreId;

        var lines = new List<JournalLineDraft>
        {
            new(receiptLedger.Id, null, settlement.RefundAmount, 0, voucher.Particulars),
            new(vendorLedgerId, party.Id, 0, settlement.RefundAmount, voucher.Particulars)
        };

        await UpsertInvoiceBankTransactionAsync(
            settlement.CompanyId,
            settlement.PaymentMode.Value,
            settlement.BankAccountId,
            TransactionType.Deposit,
            settlement.OnDate,
            settlement.SettlementNumber,
            voucher.Particulars,
            settlement.RefundAmount,
            vendor.Name,
            cancellationToken);

        var journal = await RepostSourceJournalAsync(
            "VendorRefundSettlement",
            settlement.Id,
            settlement.SettlementNumber,
            settlement.OnDate,
            settlement.DebitNoteNumber,
            voucher.Particulars,
            settlement.CompanyId,
            settlement.StoreGroupId,
            settlement.StoreId,
            lines,
            cancellationToken);

        var bankTransactionId = settlement.PaymentMode.Value == PaymentMode.Cash
            ? null
            : await db.BankTransactions
                .Where(item => item.CompanyId == settlement.CompanyId && item.Reference == settlement.SettlementNumber)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);

        return new VendorRefundPostingResult(journal.Id, bankTransactionId);
    }

    public async Task PostSalesReturnAsync(
        Invoice returnInvoice,
        Customer customer,
        Guid storeGroupId,
        decimal refundAmount,
        PaymentMode? refundPaymentMode,
        Guid? bankAccountId,
        CancellationToken cancellationToken)
    {
        var party = await EnsureCustomerPartyAsync(customer, cancellationToken);
        var customerLedgerId = party.LedgerId;
        var salesLedger = await EnsureNamedLedgerAsync(returnInvoice.CompanyId, "Sales", "Sales Accounts", LedgerCategory.SalesAccounts, LedgerType.Sale, cancellationToken);
        var outputGstLedger = await EnsureNamedLedgerAsync(returnInvoice.CompanyId, "Output GST", "Duties & Taxes", LedgerCategory.DutiesAndTaxes, LedgerType.DutyAndTax, cancellationToken);
        var roundOffLedger = await EnsureNamedLedgerAsync(returnInvoice.CompanyId, "Round Off", "Indirect Income", LedgerCategory.IndirectIncome, LedgerType.Income, cancellationToken);

        var lines = new List<JournalLineDraft>
        {
            new(salesLedger.Id, null, returnInvoice.NetAmount, 0, $"Sales return {returnInvoice.InvoiceNumber}"),
            new(customerLedgerId, party.Id, 0, returnInvoice.BillAmount, $"Credit note {returnInvoice.InvoiceNumber}")
        };

        if (returnInvoice.TaxAmount > 0)
        {
            lines.Add(new(outputGstLedger.Id, null, returnInvoice.TaxAmount, 0, $"Return tax {returnInvoice.InvoiceNumber}"));
        }

        AddRoundOff(lines, roundOffLedger.Id, returnInvoice.RoundOff * -1, isSale: true, returnInvoice.InvoiceNumber);

        if (refundAmount > 0 && refundPaymentMode.HasValue)
        {
            var settlementLedger = await ResolveSettlementLedgerAsync(returnInvoice.CompanyId, refundPaymentMode.Value, bankAccountId, cancellationToken);
            lines.Add(new(customerLedgerId, party.Id, refundAmount, 0, $"Return refund {returnInvoice.InvoiceNumber}"));
            lines.Add(new(settlementLedger.Id, null, 0, refundAmount, $"Return refund {returnInvoice.InvoiceNumber}"));
            await UpsertInvoiceBankTransactionAsync(
                returnInvoice.CompanyId,
                refundPaymentMode.Value,
                bankAccountId,
                TransactionType.Withdraw,
                returnInvoice.OnDate,
                $"SR-{returnInvoice.InvoiceNumber}",
                $"Return refund {returnInvoice.InvoiceNumber}",
                refundAmount,
                customer.Name,
                cancellationToken);
        }

        await RepostSourceJournalAsync(
            "SalesReturn",
            returnInvoice.Id,
            $"SR-{returnInvoice.InvoiceNumber}",
            returnInvoice.OnDate,
            returnInvoice.InvoiceNumber,
            $"Sales return {returnInvoice.InvoiceNumber}",
            returnInvoice.CompanyId,
            storeGroupId,
            returnInvoice.StoreId,
            lines,
            cancellationToken);
    }

    public async Task PostSalaryPaymentAsync(
        SalaryPayment payment,
        CancellationToken cancellationToken)
    {
        if (payment.Amount <= 0)
        {
            throw new ArgumentException("Salary payment amount must be greater than zero.");
        }

        var employee = await db.Employees.FirstOrDefaultAsync(item => item.Id == payment.EmployeeId, cancellationToken)
            ?? throw new InvalidOperationException("Select a valid employee.");
        var party = await EnsureEmployeePartyAsync(employee, cancellationToken);
        var debitLedger = IsAdvanceComponent(payment.SalaryComponent)
            ? await EnsureNamedLedgerAsync(payment.CompanyId, "Salary Advance", "Current Assets", LedgerCategory.CurrentAssets, LedgerType.CurrentAsset, cancellationToken)
            : await EnsureNamedLedgerAsync(payment.CompanyId, "Salary Payables", "Direct Expenses", LedgerCategory.DirectExpenses, LedgerType.Expenses, cancellationToken);
        var creditLedger = payment.PaymentMode == PaymentMode.Cash
            ? await EnsureNamedLedgerAsync(payment.CompanyId, "Cash In Hand", "Cash-in-Hand", LedgerCategory.CashInHand, LedgerType.Cash, cancellationToken)
            : await EnsureNamedLedgerAsync(payment.CompanyId, "Bank Clearing", "Bank Accounts", LedgerCategory.BankAccounts, LedgerType.BankAccount, cancellationToken);
        var reference = string.IsNullOrWhiteSpace(payment.VoucherNumber)
            ? $"SAL-{payment.Id:N}"[..16]
            : payment.VoucherNumber.Trim();
        var narration = $"{payment.SalaryComponent} - {employee.StaffName} - {payment.SalaryMonth}";

        await RepostSourceJournalAsync(
            "SalaryPayment",
            payment.Id,
            $"SP-{reference}",
            payment.OnDate,
            reference,
            narration,
            payment.CompanyId,
            payment.StoreGroupId,
            payment.StoreId,
            [
                new(debitLedger.Id, party.Id, payment.Amount, 0, narration),
                new(creditLedger.Id, null, 0, payment.Amount, narration)
            ],
            cancellationToken);
    }

    public async Task RemoveSalaryPaymentPostingAsync(Guid salaryPaymentId, CancellationToken cancellationToken)
    {
        var journal = await db.JournalEntries
            .Include(entry => entry.Lines)
            .FirstOrDefaultAsync(entry => entry.SourceType == "SalaryPayment" && entry.SourceId == salaryPaymentId, cancellationToken);

        if (journal is null)
        {
            return;
        }

        db.JournalLines.RemoveRange(journal.Lines ?? []);
        db.JournalEntries.Remove(journal);
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

    private async Task<Party?> ResolveVoucherPartyAsync(
        VoucherSaveRequest request,
        Ledger ledger,
        CancellationToken cancellationToken)
    {
        Party? party = null;
        var partyId = NormalizeGuid(request.PartyId);

        if (partyId.HasValue)
        {
            party = await db.Parties.FirstOrDefaultAsync(item => item.Id == partyId.Value, cancellationToken);
            if (party is null)
            {
                throw new InvalidOperationException("Select a valid party.");
            }

            if (party.LedgerId == Guid.Empty)
            {
                party.LedgerId = (await EnsurePartyLedgerAsync(party, cancellationToken)).Id;
            }

            return party;
        }

        if (!ledger.IsParty)
        {
            return null;
        }

        party = await db.Parties.FirstOrDefaultAsync(
            item => item.CompanyId == request.CompanyId && item.LedgerId == ledger.Id,
            cancellationToken);

        if (party is not null)
        {
            return party;
        }

        party = new Party
        {
            CompanyId = request.CompanyId,
            Name = ledger.Name,
            Category = PartyType.Others,
            LedgerId = ledger.Id
        };
        db.Parties.Add(party);
        return party;
    }


    private async Task<LedgerSyncIssue?> GetPartyLedgerSyncIssueAsync(Party party, CancellationToken cancellationToken)
    {
        var (_, _, expectedType, _) = GetPartyLedgerProfile(party.Category);
        var entityName = string.IsNullOrWhiteSpace(party.Name) ? "Unnamed party" : party.Name.Trim();

        if (party.LedgerId == Guid.Empty)
        {
            return new LedgerSyncIssue("Party", party.Id, entityName, null, "Critical", "Party is not linked to an accounting ledger.", "Run ledger sync repair.");
        }

        var ledger = await db.Ledgers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == party.LedgerId, cancellationToken);
        if (ledger is null)
        {
            return new LedgerSyncIssue("Party", party.Id, entityName, party.LedgerId, "Critical", "Party ledger link points to a missing ledger.", "Run ledger sync repair.");
        }

        if (ledger.CompanyId != party.CompanyId)
        {
            return new LedgerSyncIssue("Party", party.Id, entityName, party.LedgerId, "Critical", "Party ledger belongs to another company.", "Run ledger sync repair.");
        }

        if (!ledger.IsParty)
        {
            return new LedgerSyncIssue("Party", party.Id, entityName, party.LedgerId, "Critical", "Party is linked to a non-party ledger.", "Run ledger sync repair.");
        }

        if (ledger.LedgerType != expectedType)
        {
            return new LedgerSyncIssue("Party", party.Id, entityName, party.LedgerId, "Warning", $"Party ledger type is {ledger.LedgerType}; expected {expectedType}.", "Run ledger sync repair.");
        }

        var linkedElsewhere = await db.Parties.AsNoTracking().AnyAsync(
            item => item.CompanyId == party.CompanyId
                && item.LedgerId == ledger.Id
                && item.Id != party.Id,
            cancellationToken);
        if (linkedElsewhere)
        {
            return new LedgerSyncIssue("Party", party.Id, entityName, party.LedgerId, "Critical", "Party ledger is also linked to another party.", "Run ledger sync repair to create a separate ledger.");
        }

        if (!ledger.Name.Equals(entityName, StringComparison.Ordinal))
        {
            return new LedgerSyncIssue("Party", party.Id, entityName, party.LedgerId, "Info", $"Ledger name is '{ledger.Name}', expected '{entityName}'.", "Run ledger sync repair to rename the linked ledger.");
        }

        return null;
    }

    private async Task<LedgerSyncIssue?> GetBankAccountLedgerSyncIssueAsync(BankAccount account, CancellationToken cancellationToken)
    {
        var entityName = BuildBankLedgerName(account);
        if (account.LedgerId == Guid.Empty)
        {
            return new LedgerSyncIssue("BankAccount", account.Id, entityName, null, "Critical", "Bank account is not linked to an accounting ledger.", "Run ledger sync repair.");
        }

        var ledger = await db.Ledgers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == account.LedgerId, cancellationToken);
        if (ledger is null)
        {
            return new LedgerSyncIssue("BankAccount", account.Id, entityName, account.LedgerId, "Critical", "Bank account ledger link points to a missing ledger.", "Run ledger sync repair.");
        }

        if (ledger.CompanyId != account.CompanyId)
        {
            return new LedgerSyncIssue("BankAccount", account.Id, entityName, account.LedgerId, "Critical", "Bank account ledger belongs to another company.", "Run ledger sync repair.");
        }

        if (ledger.LedgerType != LedgerType.BankAccount)
        {
            return new LedgerSyncIssue("BankAccount", account.Id, entityName, account.LedgerId, "Critical", $"Bank account ledger type is {ledger.LedgerType}; expected BankAccount.", "Run ledger sync repair.");
        }

        var linkedElsewhere = await db.BankAccounts.AsNoTracking().AnyAsync(
            item => item.CompanyId == account.CompanyId
                && item.LedgerId == ledger.Id
                && item.Id != account.Id,
            cancellationToken);
        if (linkedElsewhere)
        {
            return new LedgerSyncIssue("BankAccount", account.Id, entityName, account.LedgerId, "Critical", "Bank account ledger is also linked to another bank account.", "Run ledger sync repair to create a separate ledger.");
        }

        if (!ledger.Name.Equals(entityName, StringComparison.Ordinal))
        {
            return new LedgerSyncIssue("BankAccount", account.Id, entityName, account.LedgerId, "Info", $"Ledger name is '{ledger.Name}', expected '{entityName}'.", "Run ledger sync repair to rename the linked ledger.");
        }

        if (ledger.IsParty)
        {
            return new LedgerSyncIssue("BankAccount", account.Id, entityName, account.LedgerId, "Warning", "Bank account is linked to a party ledger flag.", "Run ledger sync repair.");
        }

        return null;
    }

    private static (string GroupName, LedgerCategory Category, LedgerType LedgerType, string Remarks) GetPartyLedgerProfile(PartyType category)
        => category switch
        {
            PartyType.Customer or PartyType.Debitor => ("Customers", LedgerCategory.Customer, LedgerType.SundryDebtor, "Customer party ledgers"),
            PartyType.Vendor or PartyType.Supplier or PartyType.Creditor => ("Vendors", LedgerCategory.Vendor, LedgerType.SundryCreditor, "Vendor party ledgers"),
            PartyType.Employee => ("Employees", LedgerCategory.Employees, LedgerType.Employee, "Employee party ledgers"),
            _ => ("No Group", LedgerCategory.UnCategory, LedgerType.Suspense, "Default group for uncategorized and temporary party ledgers")
        };

    private async Task<bool> PartyLedgerIsLinkedElsewhereAsync(Guid ledgerId, Party party, CancellationToken cancellationToken)
        => await db.Parties.AsNoTracking().AnyAsync(
            item => item.CompanyId == party.CompanyId
                && item.LedgerId == ledgerId
                && (party.Id == Guid.Empty || item.Id != party.Id),
            cancellationToken);

    private async Task<bool> BankLedgerIsLinkedElsewhereAsync(Guid ledgerId, BankAccount account, CancellationToken cancellationToken)
        => await db.BankAccounts.AsNoTracking().AnyAsync(
            item => item.CompanyId == account.CompanyId
                && item.LedgerId == ledgerId
                && (account.Id == Guid.Empty || item.Id != account.Id),
            cancellationToken);

    private async Task<Ledger> EnsurePartyLedgerAsync(Party party, CancellationToken cancellationToken)
    {
        var (groupName, category, ledgerType, remarks) = GetPartyLedgerProfile(party.Category);
        var group = await EnsureLedgerGroupAsync(party.CompanyId, groupName, category, remarks, cancellationToken);
        var ledgerName = party.Name.Trim();
        var ledger = party.LedgerId == Guid.Empty
            ? null
            : await db.Ledgers.FirstOrDefaultAsync(item => item.Id == party.LedgerId, cancellationToken);

        if (ledger is not null
            && (ledger.CompanyId != party.CompanyId
                || !ledger.IsParty
                || await PartyLedgerIsLinkedElsewhereAsync(ledger.Id, party, cancellationToken)))
        {
            ledger = null;
        }

        if (ledger is null)
        {
            var candidates = await db.Ledgers
                .Where(item => item.CompanyId == party.CompanyId
                    && item.Name == ledgerName
                    && item.IsParty
                    && item.LedgerType == ledgerType)
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                if (!await PartyLedgerIsLinkedElsewhereAsync(candidate.Id, party, cancellationToken))
                {
                    ledger = candidate;
                    break;
                }
            }
        }

        ledger ??= new Ledger
        {
            CompanyId = party.CompanyId,
            OpeningDate = DateTime.Now,
            OpeningBalance = 0
        };

        ledger.CompanyId = party.CompanyId;
        ledger.Name = ledgerName;
        ledger.LedgerGroupId = group.Id;
        ledger.LedgerType = ledgerType;
        ledger.IsParty = true;
        if (ledger.OpeningDate == default)
        {
            ledger.OpeningDate = DateTime.Now;
        }

        if (db.Entry(ledger).State == EntityState.Detached)
        {
            db.Ledgers.Add(ledger);
        }

        return ledger;
    }

    private async Task<Party> EnsureEmployeePartyAsync(Employee employee, CancellationToken cancellationToken)
    {
        var party = await db.Parties.FirstOrDefaultAsync(
            item => item.CompanyId == employee.CompanyId && item.Name == employee.StaffName && item.Category == PartyType.Employee,
            cancellationToken);

        party ??= new Party
        {
            CompanyId = employee.CompanyId,
            Name = employee.StaffName,
            Category = PartyType.Employee
        };

        party.CompanyId = employee.CompanyId;
        party.Name = employee.StaffName;
        party.Category = PartyType.Employee;
        party.EmailId = employee.Email;
        party.Phone = employee.Mobile;
        party.PAN = employee.PAN;
        party.LedgerId = (await EnsurePartyLedgerAsync(party, cancellationToken)).Id;

        if (db.Entry(party).State == EntityState.Detached)
        {
            db.Parties.Add(party);
        }

        return party;
    }

    private async Task<Ledger> EnsureBankAccountLedgerAsync(BankAccount account, CancellationToken cancellationToken)
    {
        var group = await EnsureLedgerGroupAsync(
            account.CompanyId,
            "Bank Accounts",
            LedgerCategory.BankAccounts,
            "Current, savings, cash credit, and overdraft bank accounts",
            cancellationToken);
        var ledgerName = BuildBankLedgerName(account);
        var ledger = account.LedgerId == Guid.Empty
            ? null
            : await db.Ledgers.FirstOrDefaultAsync(item => item.Id == account.LedgerId, cancellationToken);

        if (ledger is not null
            && (ledger.CompanyId != account.CompanyId
                || ledger.LedgerType != LedgerType.BankAccount
                || await BankLedgerIsLinkedElsewhereAsync(ledger.Id, account, cancellationToken)))
        {
            ledger = null;
        }

        if (ledger is null)
        {
            var candidates = await db.Ledgers
                .Where(item => item.CompanyId == account.CompanyId
                    && item.Name == ledgerName
                    && item.LedgerType == LedgerType.BankAccount
                    && !item.IsParty)
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                if (!await BankLedgerIsLinkedElsewhereAsync(candidate.Id, account, cancellationToken))
                {
                    ledger = candidate;
                    break;
                }
            }
        }

        ledger ??= new Ledger
        {
            CompanyId = account.CompanyId,
            OpeningDate = account.OpeningDate,
            OpeningBalance = account.OpeningBalance
        };

        ledger.CompanyId = account.CompanyId;
        ledger.Name = ledgerName;
        ledger.LedgerGroupId = group.Id;
        ledger.LedgerType = LedgerType.BankAccount;
        ledger.OpeningDate = account.OpeningDate == default ? DateTime.Now : account.OpeningDate;
        ledger.OpeningBalance = account.OpeningBalance;
        ledger.IsParty = false;

        if (db.Entry(ledger).State == EntityState.Detached)
        {
            db.Ledgers.Add(ledger);
        }

        return ledger;
    }

    private async Task<LedgerGroup> EnsureLedgerGroupAsync(
        Guid companyId,
        string name,
        LedgerCategory category,
        string remarks,
        CancellationToken cancellationToken)
    {
        var group = await db.LedgerGroups.FirstOrDefaultAsync(
            item => item.CompanyId == companyId && item.Name == name,
            cancellationToken);

        if (group is not null)
        {
            return group;
        }

        group = new LedgerGroup
        {
            CompanyId = companyId,
            Name = name,
            Category = category,
            Remarks = remarks
        };
        db.LedgerGroups.Add(group);
        return group;
    }

    private static string BuildBankLedgerName(BankAccount account)
    {
        return $"{account.AccountHolderName.Trim()} - {account.AccountNumber.Trim()}";
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

    private static IReadOnlyList<SalesInvoicePaymentPosting> NormalizeSalesInvoicePaymentPostings(
        Invoice invoice,
        IReadOnlyList<SalesInvoicePaymentPosting>? payments)
    {
        var normalized = payments is null
            ? new List<SalesInvoicePaymentPosting>()
            : payments
                .Where(item => item.Amount > 0)
                .Select(item => item with { Amount = Math.Round(item.Amount, 2, MidpointRounding.AwayFromZero) })
                .ToList();

        if (normalized.Count == 0 && invoice.PaidAmount > 0 && invoice.PaymentMode.HasValue)
        {
            normalized.Add(new SalesInvoicePaymentPosting(
                invoice.PaymentMode.Value,
                invoice.PaidAmount,
                null,
                null,
                null,
                null,
                null,
                null));
        }

        return normalized;
    }

    private static IReadOnlyList<SalesInvoicePaymentPosting> NormalizeSalesInvoiceCancellationPaymentPostings(
        decimal originalPaidAmount,
        PaymentMode? originalPaymentMode,
        Guid? legacyBankAccountId,
        IReadOnlyList<SalesInvoicePaymentPosting>? originalPayments)
    {
        var normalized = originalPayments is null
            ? new List<SalesInvoicePaymentPosting>()
            : originalPayments
                .Where(item => item.Amount > 0)
                .Select(item => item with { Amount = Math.Round(item.Amount, 2, MidpointRounding.AwayFromZero) })
                .ToList();

        if (normalized.Count == 0 && originalPaidAmount > 0 && originalPaymentMode.HasValue)
        {
            normalized.Add(new SalesInvoicePaymentPosting(
                originalPaymentMode.Value,
                originalPaidAmount,
                legacyBankAccountId,
                null,
                null,
                null,
                null,
                null));
        }

        return normalized;
    }

    private async Task<Ledger> ResolveSalesInvoiceSettlementLedgerAsync(
        Guid companyId,
        SalesInvoicePaymentPosting payment,
        CancellationToken cancellationToken)
    {
        if (payment.PaymentMode == PaymentMode.Cash)
        {
            return await EnsureNamedLedgerAsync(companyId, "Cash In Hand", "Cash-in-Hand", LedgerCategory.CashInHand, LedgerType.Cash, cancellationToken);
        }

        if (BankPaymentModes.Contains(payment.PaymentMode))
        {
            return await ResolveSettlementLedgerAsync(companyId, payment.PaymentMode, payment.BankAccountId, cancellationToken);
        }

        var sourceType = CanonicalSalesInvoiceAdjustmentSource(payment);
        if (SourceMatches(sourceType, "CustomerAdvanceReceipt"))
        {
            return await EnsureNamedLedgerAsync(companyId, "Customer Advances", "Current Liabilities", LedgerCategory.CurrentLiabilities, LedgerType.CurrentLiability, cancellationToken);
        }

        if (SourceMatches(sourceType, "CreditNote") ||
            SourceMatches(sourceType, "CustomerCreditBalance") ||
            SourceMatches(sourceType, "StoreCredit") ||
            SourceMatches(sourceType, "SalesReturnCredit") ||
            payment.PaymentMode is PaymentMode.CreditNote or PaymentMode.CreditBalance or PaymentMode.SaleReturn)
        {
            return await EnsureNamedLedgerAsync(companyId, "Customer Store Credit", "Current Liabilities", LedgerCategory.CurrentLiabilities, LedgerType.CurrentLiability, cancellationToken);
        }

        if (SourceMatches(sourceType, "LoyaltyRedemption") || payment.PaymentMode == PaymentMode.Coupons)
        {
            return await EnsureNamedLedgerAsync(companyId, "Loyalty Redemption Expense", "Indirect Expenses", LedgerCategory.IndirectExpenses, LedgerType.Expenses, cancellationToken);
        }

        return await EnsureNamedLedgerAsync(companyId, "Customer Payment Adjustments", "Current Liabilities", LedgerCategory.CurrentLiabilities, LedgerType.CurrentLiability, cancellationToken);
    }

    private static string BuildSalesInvoicePaymentSourceReference(Invoice invoice, int rowNumber)
        => $"SI-{invoice.InvoiceNumber}-PAY-{rowNumber:00}";

    private static string BuildSalesInvoicePaymentNarration(Invoice invoice, SalesInvoicePaymentPosting payment, int rowNumber)
    {
        var parts = new List<string>
        {
            $"Sales receipt {invoice.InvoiceNumber}",
            $"row {rowNumber}",
            payment.PaymentMode.ToString()
        };

        AddIfPresent(parts, "ref", payment.ReferenceNumber);
        AddIfPresent(parts, "gateway", payment.GatewayReference);
        AddIfPresent(parts, "status", payment.SettlementStatus);
        AddIfPresent(parts, "source", payment.AdjustmentSourceType);
        if (payment.AdjustmentSourceId.HasValue)
        {
            parts.Add($"sourceId {payment.AdjustmentSourceId.Value}");
        }

        return string.Join(" | ", parts);
    }

    private static string CanonicalSalesInvoiceAdjustmentSource(SalesInvoicePaymentPosting payment)
    {
        if (!string.IsNullOrWhiteSpace(payment.AdjustmentSourceType))
        {
            return payment.AdjustmentSourceType.Trim();
        }

        return payment.PaymentMode switch
        {
            PaymentMode.CreditNote => "CreditNote",
            PaymentMode.CreditBalance => "CustomerCreditBalance",
            PaymentMode.SaleReturn => "SalesReturnCredit",
            PaymentMode.Coupons => "LoyaltyRedemption",
            _ => payment.PaymentMode.ToString()
        };
    }

    private static bool SourceMatches(string? sourceType, string expected)
        => string.Equals(sourceType, expected, StringComparison.OrdinalIgnoreCase);

    private static void AddIfPresent(ICollection<string> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label} {value.Trim()}");
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private async Task<Ledger> ResolveSettlementLedgerAsync(
        Guid companyId,
        PaymentMode paymentMode,
        Guid? bankAccountId,
        CancellationToken cancellationToken)
    {
        if (paymentMode == PaymentMode.Cash)
        {
            return await EnsureNamedLedgerAsync(companyId, "Cash In Hand", "Cash-in-Hand", LedgerCategory.CashInHand, LedgerType.Cash, cancellationToken);
        }

        if (!bankAccountId.HasValue || bankAccountId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Select bank account for non-cash invoice payment.");
        }

        var bankAccount = await db.BankAccounts
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Id == bankAccountId.Value, cancellationToken);

        if (bankAccount is not null)
        {
            var bankLedger = await db.Ledgers.FirstOrDefaultAsync(item => item.Id == bankAccount.LedgerId, cancellationToken);
            if (bankLedger is not null)
            {
                return bankLedger;
            }
        }

        throw new InvalidOperationException("The selected bank account is not linked to a ledger.");
    }

    private async Task UpsertInvoiceBankTransactionAsync(
        Guid companyId,
        PaymentMode paymentMode,
        Guid? bankAccountId,
        TransactionType transactionType,
        DateTime onDate,
        string reference,
        string narration,
        decimal amount,
        string personName,
        CancellationToken cancellationToken,
        string? paymentReference = null)
    {
        if (!BankPaymentModes.Contains(paymentMode) || amount <= 0)
        {
            return;
        }

        if (!bankAccountId.HasValue || bankAccountId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Select bank account for non-cash invoice payment.");
        }

        var bankAccount = await db.BankAccounts.FirstOrDefaultAsync(
            item => item.CompanyId == companyId && item.Id == bankAccountId.Value,
            cancellationToken)
            ?? throw new InvalidOperationException("Select a valid bank account.");

        var transaction = await db.BankTransactions.FirstOrDefaultAsync(
            item => item.Reference == reference,
            cancellationToken);

        transaction ??= new BankTransaction
        {
            CompanyId = companyId
        };

        transaction.CompanyId = companyId;
        transaction.BankAccountId = bankAccount.Id;
        transaction.OnDate = onDate;
        transaction.TransactionType = transactionType;
        transaction.TransactionMode = ToTransactionMode(paymentMode);
        transaction.Narration = narration;
        transaction.Reference = reference;
        transaction.Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        transaction.PersonName = personName;

        if (db.Entry(transaction).State == EntityState.Detached)
        {
            db.BankTransactions.Add(transaction);
        }

        await UpsertManualStatementLineAsync(transaction, cancellationToken);
        await UpsertInvoiceChequeLogAsync(transaction, bankAccount, paymentMode, paymentReference, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await RecalculateBankStatementBalancesAsync(bankAccount.Id, cancellationToken);
    }

    private async Task UpsertInvoiceChequeLogAsync(
        BankTransaction transaction,
        BankAccount bankAccount,
        PaymentMode paymentMode,
        string? paymentReference,
        CancellationToken cancellationToken)
    {
        var existing = await db.ChequeLogs.FirstOrDefaultAsync(
            item => item.Narration == transaction.Reference,
            cancellationToken);

        if (paymentMode != PaymentMode.Cheque)
        {
            if (existing is not null)
            {
                db.ChequeLogs.Remove(existing);
            }

            return;
        }

        existing ??= new ChequeLog
        {
            CompanyId = transaction.CompanyId
        };

        var chequeReference = string.IsNullOrWhiteSpace(paymentReference)
            ? transaction.Reference ?? string.Empty
            : paymentReference.Trim();

        existing.CompanyId = transaction.CompanyId;
        existing.BankAccountId = bankAccount.Id;
        existing.ChequeNumber = chequeReference;
        existing.CheequeNumber = chequeReference;
        existing.OnDate = transaction.OnDate;
        existing.ChequeDate = transaction.OnDate;
        existing.Narration = transaction.Reference;
        existing.ChequeBank = bankAccount.AccountNumber;
        existing.BankTransactionId = transaction.Id;
        existing.Amount = transaction.Amount;
        existing.PersonName = transaction.PersonName;
        existing.Status = transaction.TransactionType == TransactionType.Deposit ? "Deposited" : "Issued";
        existing.InHouse = false;

        if (db.Entry(existing).State == EntityState.Detached)
        {
            db.ChequeLogs.Add(existing);
        }
    }

    private sealed record PostingStoreScope(Guid StoreGroupId, Guid StoreId);

    private async Task<GstAccountingLedgerRow> BuildTaxLedgerRowAsync(
        Guid companyId,
        string ledgerName,
        DateTime periodStart,
        DateTime periodEndExclusive,
        bool creditPositive,
        string meaning,
        CancellationToken cancellationToken)
    {
        var totals = await db.JournalLines
            .AsNoTracking()
            .Where(line => line.CompanyId == companyId
                && line.Ledger != null
                && line.Ledger.Name == ledgerName
                && line.JournalEntry != null
                && line.JournalEntry.OnDate >= periodStart
                && line.JournalEntry.OnDate < periodEndExclusive)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Debit = group.Sum(line => line.Debit),
                Credit = group.Sum(line => line.Credit)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var debit = Math.Round(totals?.Debit ?? 0, 2, MidpointRounding.AwayFromZero);
        var credit = Math.Round(totals?.Credit ?? 0, 2, MidpointRounding.AwayFromZero);
        var net = creditPositive ? credit - debit : debit - credit;

        return new GstAccountingLedgerRow(
            ledgerName,
            debit,
            credit,
            Math.Round(net, 2, MidpointRounding.AwayFromZero),
            meaning);
    }

    private async Task<PostingStoreScope> ResolvePostingStoreAsync(
        Guid companyId,
        Guid? storeGroupId,
        Guid? storeId,
        CancellationToken cancellationToken)
    {
        if (storeId.HasValue && storeId.Value != Guid.Empty)
        {
            var store = await db.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == storeId.Value && item.CompanyId == companyId, cancellationToken);

            if (store is null)
            {
                throw new InvalidOperationException("Selected store is not valid for this company.");
            }

            if (storeGroupId.HasValue && storeGroupId.Value != Guid.Empty && store.StoreGroupId != storeGroupId.Value)
            {
                throw new InvalidOperationException("Selected store does not belong to the selected store group.");
            }

            return new PostingStoreScope(store.StoreGroupId, store.Id);
        }

        if (storeGroupId.HasValue && storeGroupId.Value != Guid.Empty)
        {
            var store = await db.Stores
                .AsNoTracking()
                .Where(item => item.CompanyId == companyId && item.StoreGroupId == storeGroupId.Value && !item.Deleted)
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (store is not null)
            {
                return new PostingStoreScope(store.StoreGroupId, store.Id);
            }
        }

        var defaultStore = await db.Stores
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId && !item.Deleted)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (defaultStore is null)
        {
            throw new InvalidOperationException("Create at least one store before posting GST accounting entries.");
        }

        return new PostingStoreScope(defaultStore.StoreGroupId, defaultStore.Id);
    }

    private static string GstAccountingReference(string returnPeriod)
    {
        var normalized = string.IsNullOrWhiteSpace(returnPeriod)
            ? DateTime.Now.ToString("yyyyMM")
            : returnPeriod.Trim().ToUpperInvariant().Replace(" ", string.Empty);

        return $"GST-RET-{normalized}";
    }

    private static string InventoryLedgerName(string? storeCode, string storeName)
    {
        var identity = string.IsNullOrWhiteSpace(storeCode) ? storeName.Trim() : storeCode.Trim().ToUpperInvariant();
        return $"Inventory - {identity}";
    }

    private static Guid DeterministicGuid(string seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    private async Task<Ledger> EnsureNamedLedgerAsync(
        Guid companyId,
        string name,
        string groupName,
        LedgerCategory category,
        LedgerType ledgerType,
        CancellationToken cancellationToken)
    {
        var ledger = await db.Ledgers.FirstOrDefaultAsync(
            item => item.CompanyId == companyId && item.Name == name,
            cancellationToken);

        if (ledger is not null)
        {
            return ledger;
        }

        var group = await EnsureLedgerGroupAsync(companyId, groupName, category, $"{groupName} ledgers", cancellationToken);
        ledger = new Ledger
        {
            CompanyId = companyId,
            Name = name,
            LedgerGroupId = group.Id,
            LedgerType = ledgerType,
            OpeningDate = DateTime.Now,
            OpeningBalance = 0,
            IsParty = false
        };
        db.Ledgers.Add(ledger);
        return ledger;
    }

    private async Task<JournalEntry> RepostSourceJournalAsync(
        string sourceType,
        Guid sourceId,
        string entryNumber,
        DateTime onDate,
        string referenceNumber,
        string narration,
        Guid companyId,
        Guid storeGroupId,
        Guid storeId,
        IReadOnlyList<JournalLineDraft> lines,
        CancellationToken cancellationToken)
    {
        var debit = lines.Sum(item => item.Debit);
        var credit = lines.Sum(item => item.Credit);
        if (Math.Round(debit - credit, 2) != 0)
        {
            throw new InvalidOperationException($"Accounting entry is not balanced for {referenceNumber}.");
        }

        var existing = await db.JournalEntries
            .Include(entry => entry.Lines)
            .FirstOrDefaultAsync(entry => entry.SourceType == sourceType && entry.SourceId == sourceId, cancellationToken);

        if (existing is not null)
        {
            db.JournalLines.RemoveRange(existing.Lines ?? []);
            existing.EntryNumber = entryNumber;
            existing.OnDate = onDate;
            existing.ReferenceNumber = referenceNumber;
            existing.Narration = narration;
            existing.CompanyId = companyId;
            existing.StoreGroupId = storeGroupId;
            existing.StoreId = storeId;
            existing.PostedAt = DateTime.Now;
        }
        else
        {
            existing = new JournalEntry
            {
                EntryNumber = entryNumber,
                OnDate = onDate,
                SourceType = sourceType,
                SourceId = sourceId,
                ReferenceNumber = referenceNumber,
                Narration = narration,
                CompanyId = companyId,
                StoreGroupId = storeGroupId,
                StoreId = storeId
            };
            db.JournalEntries.Add(existing);
        }

        db.JournalLines.AddRange(lines
            .Where(line => line.Debit > 0 || line.Credit > 0)
            .Select(line => new JournalLine
            {
                JournalEntryId = existing.Id,
                LedgerId = line.LedgerId,
                PartyId = line.PartyId,
                Debit = line.Debit,
                Credit = line.Credit,
                Narration = line.Narration,
                CompanyId = companyId,
                StoreGroupId = line.StoreGroupId ?? storeGroupId,
                StoreId = line.StoreId ?? storeId
            }));

        return existing;
    }

    private static void AddRoundOff(
        List<JournalLineDraft> lines,
        Guid roundOffLedgerId,
        decimal roundOff,
        bool isSale,
        string referenceNumber)
    {
        if (roundOff == 0)
        {
            return;
        }

        if (isSale)
        {
            if (roundOff > 0)
            {
                lines.Add(new JournalLineDraft(roundOffLedgerId, null, 0, roundOff, $"Round off {referenceNumber}"));
            }
            else
            {
                lines.Add(new JournalLineDraft(roundOffLedgerId, null, Math.Abs(roundOff), 0, $"Round off {referenceNumber}"));
            }

            return;
        }

        if (roundOff > 0)
        {
            lines.Add(new JournalLineDraft(roundOffLedgerId, null, roundOff, 0, $"Round off {referenceNumber}"));
        }
        else
        {
            lines.Add(new JournalLineDraft(roundOffLedgerId, null, 0, Math.Abs(roundOff), $"Round off {referenceNumber}"));
        }
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

    private async Task UpsertManualChequeLogAsync(
        BankTransaction transaction,
        BankAccount bankAccount,
        CancellationToken cancellationToken)
    {
        var existing = await db.ChequeLogs.FirstOrDefaultAsync(
            item => item.Narration == transaction.Reference,
            cancellationToken);

        if (transaction.TransactionMode != TransactionMode.Cheque)
        {
            if (existing is not null)
            {
                db.ChequeLogs.Remove(existing);
            }

            return;
        }

        existing ??= new ChequeLog
        {
            CompanyId = transaction.CompanyId
        };

        var chequeReference = transaction.Reference ?? string.Empty;

        existing.CompanyId = transaction.CompanyId;
        existing.BankAccountId = bankAccount.Id;
        existing.ChequeNumber = chequeReference;
        existing.CheequeNumber = chequeReference;
        existing.OnDate = transaction.OnDate;
        existing.ChequeDate = transaction.OnDate;
        existing.Narration = transaction.Reference;
        existing.ChequeBank = bankAccount.AccountNumber;
        existing.BankTransactionId = transaction.Id;
        existing.Amount = transaction.Amount;
        existing.PersonName = transaction.PersonName;
        existing.Status = transaction.TransactionType == TransactionType.Deposit ? "Deposited" : "Issued";
        existing.InHouse = false;

        if (db.Entry(existing).State == EntityState.Detached)
        {
            db.ChequeLogs.Add(existing);
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

    private static string? CleanText(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string NormalizeChequeStatus(string? value)
    {
        var text = value?.Trim();
        return text switch
        {
            "Issued" or "Deposited" or "Cleared" or "Bounced" or "Cancelled" => text,
            "Clear" or "Clearing" => "Cleared",
            "Bounce" or "Returned" => "Bounced",
            "Cancel" => "Cancelled",
            _ => throw new ArgumentException("Cheque status must be Issued, Deposited, Cleared, Bounced, or Cancelled.")
        };
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

    private static bool IsAdvanceComponent(SalaryComponent component)
    {
        return component is SalaryComponent.Advance or SalaryComponent.SalaryAdvance;
    }

    private static Guid? NormalizeGuid(Guid? value)
    {
        return value.HasValue && value.Value != Guid.Empty ? value.Value : null;
    }

    private static void ValidateVoucher(VoucherSaveRequest request)
    {
        if (request.Id.HasValue && string.IsNullOrWhiteSpace(request.VoucherNumber))
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
