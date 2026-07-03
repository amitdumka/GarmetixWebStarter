using System.Globalization;
using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.BankReconciliation;

public static class BankReconciliationClosureEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const int EvidenceLimit = 500;

    public static RouteGroupBuilder MapBankReconciliationClosureEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bank-reconciliation/settlement-closure")
            .WithTags("Bank Reconciliation")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("", GetStatusAsync);
        group.MapGet("/evidence.csv", ExportEvidenceCsvAsync);

        return group;
    }

    private static async Task<BankReconciliationClosureReportDto> GetStatusAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildReportAsync(context, db, companyId, storeGroupId, storeId, from, to, cancellationToken);
    }

    private static async Task<IResult> ExportEvidenceCsvAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(context, db, companyId, storeGroupId, storeId, from, to, cancellationToken);
        var csv = BuildCsv(report);
        var fileName = $"garmetix-bank-reconciliation-closure-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<BankReconciliationClosureReportDto> BuildReportAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate, toExclusive) = ResolveDateRange(from, to);
        var issues = new List<BankReconciliationIssueDto>();
        var settlementRows = new List<BankSettlementEvidenceRowDto>();
        var modeSummary = new Dictionary<string, BankReconciliationModeSummaryDto>(StringComparer.OrdinalIgnoreCase);

        var bankAccountsQuery = WorkspaceScope.ApplyTo(db.BankAccounts.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        if (companyId.HasValue) bankAccountsQuery = bankAccountsQuery.Where(item => item.CompanyId == companyId.Value);
        var bankAccounts = await bankAccountsQuery.ToListAsync(cancellationToken);
        var bankAccountIds = bankAccounts.Select(item => item.Id).Distinct().ToList();
        var bankAccountById = bankAccounts.ToDictionary(item => item.Id, item => item);

        var bankTransactionsQuery = WorkspaceScope.ApplyTo(db.BankTransactions.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) bankTransactionsQuery = bankTransactionsQuery.Where(item => item.CompanyId == companyId.Value);
        if (bankAccountIds.Count > 0) bankTransactionsQuery = bankTransactionsQuery.Where(item => bankAccountIds.Contains(item.BankAccountId));
        var bankTransactions = await bankTransactionsQuery.ToListAsync(cancellationToken);
        var bankTransactionIds = bankTransactions.Select(item => item.Id).ToList();

        var statementQuery = WorkspaceScope.ApplyTo(db.BankStatementLines.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) statementQuery = statementQuery.Where(item => item.CompanyId == companyId.Value);
        if (bankAccountIds.Count > 0) statementQuery = statementQuery.Where(item => bankAccountIds.Contains(item.BankAccountId));
        var statementLines = await statementQuery.ToListAsync(cancellationToken);

        var bankJournalSourceIds = bankTransactionIds.Count == 0
            ? new HashSet<Guid>()
            : (await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceType == "BankTransaction" && item.SourceId.HasValue && bankTransactionIds.Contains(item.SourceId.Value))
                .Select(item => item.SourceId!.Value)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var salesQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && !item.ReturnInvoice && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) salesQuery = salesQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeId.HasValue) salesQuery = salesQuery.Where(item => item.StoreId == storeId.Value);
        var sales = await salesQuery.ToListAsync(cancellationToken);
        var saleIds = sales.Select(item => item.Id).Distinct().ToList();
        var saleById = sales.ToDictionary(item => item.Id, item => item);

        var salePayments = saleIds.Count == 0
            ? new List<InvoicePayment>()
            : await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive && saleIds.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var purchaseQuery = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) purchaseQuery = purchaseQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) purchaseQuery = purchaseQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) purchaseQuery = purchaseQuery.Where(item => item.StoreId == storeId.Value);
        var purchases = await purchaseQuery.ToListAsync(cancellationToken);
        var purchaseIds = purchases.Select(item => item.Id).Distinct().ToList();
        var purchaseById = purchases.ToDictionary(item => item.Id, item => item);

        var purchasePayments = purchaseIds.Count == 0
            ? new List<PurchasePayment>()
            : await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive && purchaseIds.Contains(item.PurchaseInvoiceId))
                .ToListAsync(cancellationToken);

        var customerAdvanceQuery = WorkspaceScope.ApplyTo(db.CustomerAdvanceReceipts.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) customerAdvanceQuery = customerAdvanceQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) customerAdvanceQuery = customerAdvanceQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) customerAdvanceQuery = customerAdvanceQuery.Where(item => item.StoreId == storeId.Value);
        var customerAdvances = await customerAdvanceQuery.ToListAsync(cancellationToken);

        var vouchersQuery = WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) vouchersQuery = vouchersQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) vouchersQuery = vouchersQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) vouchersQuery = vouchersQuery.Where(item => item.StoreId == storeId.Value);
        var vouchers = await vouchersQuery.ToListAsync(cancellationToken);

        var salaryQuery = WorkspaceScope.ApplyTo(db.SalaryPayments.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) salaryQuery = salaryQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) salaryQuery = salaryQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) salaryQuery = salaryQuery.Where(item => item.StoreId == storeId.Value);
        var salaryPayments = await salaryQuery.ToListAsync(cancellationToken);
        var salaryPaymentIds = salaryPayments.Select(item => item.Id).Distinct().ToList();
        var salaryJournalIds = salaryPaymentIds.Count == 0
            ? new HashSet<Guid>()
            : (await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceType == "SalaryPayment" && item.SourceId.HasValue && salaryPaymentIds.Contains(item.SourceId.Value))
                .Select(item => item.SourceId!.Value)
                .ToListAsync(cancellationToken)).ToHashSet();

        foreach (var payment in salePayments.Where(item => RequiresBankSettlement(item.PaymentMode, item.AdjustmentSourceType)))
        {
            saleById.TryGetValue(payment.InvoiceId, out var invoice);
            var row = BuildSettlementRow(
                "In",
                "Sale receipt",
                payment.Id,
                invoice?.InvoiceNumber ?? payment.InvoiceId.ToString("N"),
                payment.OnDate,
                invoice?.CustomerName ?? invoice?.CustomerMobileNumber ?? "Customer",
                payment.PaymentMode.ToString(),
                payment.Amount,
                payment.BankAccountId,
                payment.ReferenceNumber ?? payment.GatewayReference,
                payment.AdjustmentSourceType,
                bankAccountById,
                bankTransactions,
                statementLines);
            settlementRows.Add(row);
            AddSettlementIssues(issues, row, "sale receipt", "/billing/final-qa");
            AddMode(modeSummary, row);
        }

        foreach (var payment in purchasePayments.Where(item => RequiresBankSettlement(item.PaymentMode, item.AdjustmentSourceType)))
        {
            purchaseById.TryGetValue(payment.PurchaseInvoiceId, out var invoice);
            var row = BuildSettlementRow(
                "Out",
                "Purchase/vendor payment",
                payment.Id,
                invoice?.InwardNumber ?? invoice?.InvoiceNumber ?? payment.PurchaseInvoiceId.ToString("N"),
                payment.OnDate,
                invoice?.VendorName ?? "Vendor",
                payment.PaymentMode.ToString(),
                payment.Amount,
                payment.BankAccountId,
                payment.ReferenceNumber,
                payment.AdjustmentSourceType,
                bankAccountById,
                bankTransactions,
                statementLines);
            settlementRows.Add(row);
            AddSettlementIssues(issues, row, "vendor payment", "/purchase/vendor-payable-reconciliation");
            AddMode(modeSummary, row);
        }

        foreach (var receipt in customerAdvances.Where(item => RequiresBankSettlement(item.PaymentMode, null)))
        {
            var row = BuildSettlementRow(
                "In",
                "Customer advance",
                receipt.Id,
                receipt.ReceiptNumber,
                receipt.OnDate,
                receipt.CustomerName,
                receipt.PaymentMode.ToString(),
                receipt.Amount,
                receipt.BankAccountId,
                receipt.ReferenceNumber,
                "CustomerAdvance",
                bankAccountById,
                bankTransactions,
                statementLines);
            settlementRows.Add(row);
            AddSettlementIssues(issues, row, "customer advance", "/customers/dues-reconciliation");
            AddMode(modeSummary, row);
        }

        foreach (var voucher in vouchers.Where(item => RequiresBankSettlement(item.PaymentMode, null)))
        {
            var bankAccountId = voucher.AccountNumber;
            var row = BuildSettlementRow(
                voucher.VoucherType == VoucherType.Receipt ? "In" : "Out",
                $"Voucher {voucher.VoucherType}",
                voucher.Id,
                voucher.VoucherNumber,
                voucher.OnDate,
                voucher.PartyName,
                voucher.PaymentMode.ToString(),
                voucher.Amount,
                bankAccountId,
                voucher.SlipNumber ?? voucher.PaymentDetails,
                "Voucher",
                bankAccountById,
                bankTransactions,
                statementLines);
            settlementRows.Add(row);
            AddSettlementIssues(issues, row, "voucher", "/vouchers");
            AddMode(modeSummary, row);
        }

        foreach (var payment in salaryPayments.Where(item => RequiresBankSettlement(item.PaymentMode, null)))
        {
            var hasJournal = salaryJournalIds.Contains(payment.Id);
            var row = new BankSettlementEvidenceRowDto(
                payment.Id,
                "Out",
                "Salary payment",
                payment.VoucherNumber,
                payment.OnDate,
                payment.Employee?.FullName ?? payment.Remarks ?? payment.EmployeeId.ToString("N"),
                payment.PaymentMode.ToString(),
                Round(payment.Amount),
                null,
                string.Empty,
                string.Empty,
                "SalaryPayment",
                hasJournal,
                hasJournal,
                hasJournal,
                hasJournal,
                hasJournal ? "Posted salary journal evidence found." : "Salary payment has no linked bank account field; confirm salary payment journal/bank proof manually.",
                "/payroll/finalization");
            settlementRows.Add(row);
            if (!hasJournal)
            {
                issues.Add(new BankReconciliationIssueDto("Warning", "SALARY_BANK_PROOF_MANUAL", row.SourceType, row.SourceId, $"Salary payment {row.SourceNumber} needs bank proof/journal evidence.", row.ActionPath));
            }
            AddMode(modeSummary, row);
        }

        foreach (var transaction in bankTransactions)
        {
            var hasStatementLine = statementLines.Any(item => item.BankTransactionId == transaction.Id);
            var hasJournal = bankJournalSourceIds.Contains(transaction.Id);
            if (!transaction.Reconciled && !hasStatementLine)
            {
                issues.Add(new BankReconciliationIssueDto("Warning", "BANK_TRANSACTION_UNRECONCILED", "BankTransaction", transaction.Id, $"Bank transaction {transaction.Reference ?? transaction.Id.ToString("N")} is not reconciled and has no linked statement line.", "/accounting"));
            }
            if (!hasJournal)
            {
                issues.Add(new BankReconciliationIssueDto("Critical", "BANK_TRANSACTION_JOURNAL_MISSING", "BankTransaction", transaction.Id, $"Bank transaction {transaction.Reference ?? transaction.Id.ToString("N")} has no accounting journal.", "/accounting"));
            }
        }

        foreach (var line in statementLines.Where(item => !item.Reconciled && item.BankTransactionId is null))
        {
            issues.Add(new BankReconciliationIssueDto("Warning", "BANK_STATEMENT_LINE_UNMATCHED", "BankStatementLine", line.Id, $"Bank statement line {line.Reference ?? line.Description} is not reconciled to a bank transaction.", "/accounting"));
        }

        var criticalIssues = issues.Count(item => item.Severity == "Critical");
        var warningIssues = issues.Count(item => item.Severity == "Warning");
        var status = criticalIssues == 0 ? "Complete" : "Not Complete";
        var inboundTotal = Round(settlementRows.Where(item => item.Direction == "In").Sum(item => item.Amount));
        var outboundTotal = Round(settlementRows.Where(item => item.Direction == "Out").Sum(item => item.Amount));
        var matchedTotal = Round(settlementRows.Where(item => item.MatchedBankTransaction || item.Reconciled).Sum(item => item.Amount));
        var bankTransactionDeposit = Round(bankTransactions.Where(item => item.TransactionType == TransactionType.Deposit).Sum(item => item.Amount));
        var bankTransactionWithdraw = Round(bankTransactions.Where(item => item.TransactionType == TransactionType.Withdraw).Sum(item => item.Amount));
        var statementCredit = Round(statementLines.Sum(item => item.Credit));
        var statementDebit = Round(statementLines.Sum(item => item.Debit));

        var metrics = new List<BankReconciliationMetricDto>
        {
            new("Non-cash settlement rows", settlementRows.Count, null, "Sale receipts, vendor payments, customer advances, vouchers and salary payments requiring bank proof."),
            new("Inbound settlement total", null, inboundTotal, "Expected bank credits from non-cash receipts."),
            new("Outbound settlement total", null, outboundTotal, "Expected bank debits from non-cash payments."),
            new("Matched/reconciled settlement total", null, matchedTotal, "Rows with a matched bank transaction, statement line or source journal evidence."),
            new("Bank transaction deposits", null, bankTransactionDeposit, "Deposit bank transactions posted in the selected period."),
            new("Bank transaction withdrawals", null, bankTransactionWithdraw, "Withdrawal bank transactions posted in the selected period."),
            new("Statement credits", null, statementCredit, "Imported/entered statement credit total."),
            new("Statement debits", null, statementDebit, "Imported/entered statement debit total."),
            new("Unreconciled bank transactions", bankTransactions.Count(item => !item.Reconciled), null, "Bank transactions still not marked reconciled."),
            new("Unmatched statement lines", statementLines.Count(item => !item.Reconciled && item.BankTransactionId is null), null, "Statement rows not linked to a bank transaction.")
        };

        var bankRows = bankAccounts
            .OrderBy(item => item.AccountHolderName)
            .ThenBy(item => item.AccountNumber)
            .Select(account =>
            {
                var txRows = bankTransactions.Where(item => item.BankAccountId == account.Id).ToList();
                var stRows = statementLines.Where(item => item.BankAccountId == account.Id).ToList();
                var settlementForBank = settlementRows.Where(item => item.BankAccountId == account.Id).ToList();
                return new BankAccountReconciliationRowDto(
                    account.Id,
                    account.AccountHolderName,
                    MaskAccount(account.AccountNumber),
                    account.Active,
                    settlementForBank.Count,
                    Round(settlementForBank.Sum(item => item.Amount)),
                    txRows.Count,
                    Round(txRows.Where(item => item.TransactionType == TransactionType.Deposit).Sum(item => item.Amount)),
                    Round(txRows.Where(item => item.TransactionType == TransactionType.Withdraw).Sum(item => item.Amount)),
                    stRows.Count,
                    Round(stRows.Sum(item => item.Credit)),
                    Round(stRows.Sum(item => item.Debit)),
                    txRows.Count(item => !item.Reconciled),
                    stRows.Count(item => !item.Reconciled && item.BankTransactionId is null));
            })
            .ToList();

        return new BankReconciliationClosureReportDto(
            status,
            fromDate,
            toDate,
            criticalIssues,
            warningIssues,
            metrics,
            modeSummary.Values.OrderBy(item => item.PaymentMode).ToList(),
            bankRows,
            settlementRows.OrderByDescending(item => item.OnDate).ThenBy(item => item.SourceType).Take(EvidenceLimit).ToList(),
            issues.OrderBy(item => item.Severity == "Critical" ? 0 : 1).ThenBy(item => item.Code).Take(EvidenceLimit).ToList(),
            BuildCloseoutChecklist(criticalIssues, warningIssues),
            BuildOperatorRules(),
            BuildKnownLimitations(),
            BuildNextModules());
    }

    private static BankSettlementEvidenceRowDto BuildSettlementRow(
        string direction,
        string sourceType,
        Guid sourceId,
        string sourceNumber,
        DateTime onDate,
        string partyName,
        string paymentMode,
        decimal amount,
        Guid? bankAccountId,
        string? referenceNumber,
        string? adjustmentSourceType,
        IReadOnlyDictionary<Guid, BankAccount> bankAccountById,
        IReadOnlyCollection<BankTransaction> bankTransactions,
        IReadOnlyCollection<BankStatementLine> statementLines)
    {
        var hasBank = bankAccountId.HasValue && bankAccountId.Value != Guid.Empty;
        var hasReference = !string.IsNullOrWhiteSpace(referenceNumber);
        var bankName = string.Empty;
        if (bankAccountId.HasValue && bankAccountById.TryGetValue(bankAccountId.Value, out var account))
        {
            bankName = $"{account.AccountHolderName} {MaskAccount(account.AccountNumber)}".Trim();
        }

        var matchingBankTransactions = hasBank
            ? bankTransactions.Where(item => item.BankAccountId == bankAccountId.Value && Math.Abs(item.Amount - amount) <= AmountTolerance && Math.Abs((item.OnDate.Date - onDate.Date).TotalDays) <= 7).ToList()
            : new List<BankTransaction>();
        if (matchingBankTransactions.Count > 1 && hasReference)
        {
            matchingBankTransactions = matchingBankTransactions.Where(item => ContainsReference(item.Reference, referenceNumber) || ContainsReference(item.Narration, referenceNumber)).DefaultIfEmpty(matchingBankTransactions.First()).ToList();
        }

        var matchedTx = matchingBankTransactions.FirstOrDefault();
        var matchedStatement = matchedTx is not null
            ? statementLines.FirstOrDefault(item => item.BankTransactionId == matchedTx.Id)
            : null;
        matchedStatement ??= hasBank
            ? statementLines.FirstOrDefault(item => item.BankAccountId == bankAccountId.Value && Math.Abs(Math.Max(item.Credit, item.Debit) - amount) <= AmountTolerance && Math.Abs((item.OnDate.Date - onDate.Date).TotalDays) <= 7 && (string.IsNullOrWhiteSpace(referenceNumber) || ContainsReference(item.Reference, referenceNumber) || ContainsReference(item.Description, referenceNumber)))
            : null;

        var matched = matchedTx is not null;
        var reconciled = matchedTx?.Reconciled == true || matchedStatement?.Reconciled == true || matchedStatement?.BankTransactionId is not null;
        var notes = hasBank
            ? matched
                ? reconciled ? "Matched and reconciled with bank transaction/statement evidence." : "Matched bank transaction found; reconciliation flag or statement link is pending."
                : matchedStatement is not null ? "Matched statement line found; create/link bank transaction if needed." : "No matching bank transaction/statement evidence found in selected range."
            : "Bank account mapping is missing.";

        return new BankSettlementEvidenceRowDto(
            sourceId,
            direction,
            sourceType,
            sourceNumber,
            onDate,
            partyName,
            paymentMode,
            Round(amount),
            bankAccountId,
            bankName,
            referenceNumber ?? string.Empty,
            adjustmentSourceType ?? string.Empty,
            hasBank,
            hasReference,
            matched || matchedStatement is not null,
            reconciled,
            notes,
            ActionPathForSource(sourceType));
    }

    private static void AddSettlementIssues(List<BankReconciliationIssueDto> issues, BankSettlementEvidenceRowDto row, string label, string actionPath)
    {
        if (!row.HasBankAccount)
        {
            issues.Add(new BankReconciliationIssueDto("Critical", "BANK_ACCOUNT_MISSING", row.SourceType, row.SourceId, $"Non-cash {label} {row.SourceNumber} has no bank account mapping.", actionPath));
        }
        if (!row.HasReference)
        {
            issues.Add(new BankReconciliationIssueDto("Warning", "BANK_REFERENCE_MISSING", row.SourceType, row.SourceId, $"Non-cash {label} {row.SourceNumber} has no UTR/slip/gateway/reference number.", actionPath));
        }
        if (row.HasBankAccount && !row.MatchedBankTransaction)
        {
            issues.Add(new BankReconciliationIssueDto("Warning", "BANK_SETTLEMENT_UNMATCHED", row.SourceType, row.SourceId, $"Non-cash {label} {row.SourceNumber} is not matched with a bank transaction or statement line.", actionPath));
        }
    }

    private static void AddMode(IDictionary<string, BankReconciliationModeSummaryDto> summary, BankSettlementEvidenceRowDto row)
    {
        if (!summary.TryGetValue(row.PaymentMode, out var current))
        {
            current = new BankReconciliationModeSummaryDto(row.PaymentMode, 0, 0, 0, 0, 0, 0);
        }
        summary[row.PaymentMode] = current with
        {
            Count = current.Count + 1,
            Amount = Round(current.Amount + row.Amount),
            MissingBankAccount = current.MissingBankAccount + (row.HasBankAccount ? 0 : 1),
            MissingReference = current.MissingReference + (row.HasReference ? 0 : 1),
            Matched = current.Matched + (row.MatchedBankTransaction ? 1 : 0),
            Reconciled = current.Reconciled + (row.Reconciled ? 1 : 0)
        };
    }

    private static bool RequiresBankSettlement(PaymentMode mode, string? adjustmentSourceType)
    {
        if (!string.IsNullOrWhiteSpace(adjustmentSourceType) &&
            (adjustmentSourceType.Contains("Credit", StringComparison.OrdinalIgnoreCase) ||
             adjustmentSourceType.Contains("Return", StringComparison.OrdinalIgnoreCase) ||
             adjustmentSourceType.Contains("Debit", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return mode is PaymentMode.Card
            or PaymentMode.UPI
            or PaymentMode.Wallets
            or PaymentMode.IMPS
            or PaymentMode.RTGS
            or PaymentMode.NEFT
            or PaymentMode.Cheque
            or PaymentMode.DemandDraft
            or PaymentMode.Others
            or PaymentMode.MixPayments;
    }

    private static bool ContainsReference(string? candidate, string? reference)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(reference)) return false;
        var cleanCandidate = candidate.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        var cleanReference = reference.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        return cleanCandidate.Contains(cleanReference, StringComparison.OrdinalIgnoreCase) || cleanReference.Contains(cleanCandidate, StringComparison.OrdinalIgnoreCase);
    }

    private static string ActionPathForSource(string sourceType)
    {
        return sourceType switch
        {
            "Sale receipt" => "/billing/final-qa",
            "Purchase/vendor payment" => "/purchase/vendor-payable-reconciliation",
            "Customer advance" => "/customers/dues-reconciliation",
            "Salary payment" => "/payroll/finalization",
            _ when sourceType.StartsWith("Voucher", StringComparison.OrdinalIgnoreCase) => "/vouchers",
            _ => "/accounting"
        };
    }

    private static (DateTime From, DateTime To, DateTime ToExclusive) ResolveDateRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var fromDate = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var toDate = (to ?? today).Date;
        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }
        return (fromDate, toDate, toDate.AddDays(1));
    }

    private static string MaskAccount(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) return string.Empty;
        var clean = accountNumber.Trim();
        return clean.Length <= 4 ? clean : $"****{clean[^4..]}";
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static IReadOnlyList<string> BuildCloseoutChecklist(int criticalIssues, int warningIssues)
    {
        var items = new List<string>
        {
            "Every UPI/Card/NEFT/RTGS/IMPS/Cheque receipt has a bank account and reference number.",
            "Every non-cash vendor/customer/voucher/salary payment has a bank proof trail.",
            "Bank transactions are posted with journals and linked to statement lines where available.",
            "Unmatched statement lines are reviewed before month/FY close.",
            "CSV evidence is exported and kept with Day Book/FY closeout proof."
        };
        if (criticalIssues > 0) items.Insert(0, "Clear all Critical bank mapping/journal issues before closing the period.");
        if (warningIssues > 0) items.Add("Owner/accountant should accept remaining Warning rows with written remarks before locking the period.");
        return items;
    }

    private static IReadOnlyList<string> BuildOperatorRules()
    {
        return new[]
        {
            "Do not mark a period closed when non-cash rows are missing bank account mapping.",
            "Do not silently change old payment modes; correct through controlled edit/reversal with audit trail.",
            "Match UPI/Card/NEFT/RTGS/IMPS receipts with bank statement or gateway settlement proof.",
            "Use one bank account consistently for each POS/UPI settlement account.",
            "Keep exported bank reconciliation CSV with monthly closeout documents."
        };
    }

    private static IReadOnlyList<string> BuildKnownLimitations()
    {
        return new[]
        {
            "This stage validates existing settlement evidence; it does not auto-import bank statements.",
            "Exact gateway batch settlement matching can still require manual review where multiple receipts settle as one bank credit.",
            "SalaryPayment does not currently carry a BankAccountId field, so salary bank proof is validated through journal/manual evidence.",
            "Cash payments are outside this dashboard and remain covered by Store Operations, Petty Cash and Cash Details pages."
        };
    }

    private static IReadOnlyList<string> BuildNextModules()
    {
        return new[]
        {
            "Bank statement import and auto-match workflow",
            "Monthly owner closeout packet with Day Book + bank + GST evidence",
            "Profit/Loss invoice-wise and item-wise final reporting"
        };
    }

    private static string BuildCsv(BankReconciliationClosureReportDto report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Section,Field,Value");
        AppendCsvRow(builder, "Summary", "Status", report.Status);
        AppendCsvRow(builder, "Summary", "From", report.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AppendCsvRow(builder, "Summary", "To", report.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AppendCsvRow(builder, "Summary", "Critical Issues", report.CriticalIssues.ToString(CultureInfo.InvariantCulture));
        AppendCsvRow(builder, "Summary", "Warning Issues", report.WarningIssues.ToString(CultureInfo.InvariantCulture));
        foreach (var metric in report.Metrics)
        {
            AppendCsvRow(builder, "Metric", metric.Label, metric.Amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? metric.Count?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        builder.AppendLine();
        builder.AppendLine("Mode Summary,Payment Mode,Count,Amount,Missing Bank,Missing Reference,Matched,Reconciled");
        foreach (var row in report.PaymentModeSummary)
        {
            AppendCsvRow(builder, "Mode Summary", row.PaymentMode, row.Count.ToString(CultureInfo.InvariantCulture), row.Amount.ToString("0.00", CultureInfo.InvariantCulture), row.MissingBankAccount.ToString(CultureInfo.InvariantCulture), row.MissingReference.ToString(CultureInfo.InvariantCulture), row.Matched.ToString(CultureInfo.InvariantCulture), row.Reconciled.ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine();
        builder.AppendLine("Bank Account,Holder,Account,Active,Settlement Rows,Settlement Amount,Bank Tx Rows,Deposit,Withdraw,Statement Rows,Statement Credit,Statement Debit,Unreconciled Tx,Unmatched Statement");
        foreach (var row in report.BankAccounts)
        {
            AppendCsvRow(builder, "Bank Account", row.AccountHolderName, row.MaskedAccountNumber, row.Active ? "Yes" : "No", row.SettlementRows.ToString(CultureInfo.InvariantCulture), row.SettlementAmount.ToString("0.00", CultureInfo.InvariantCulture), row.BankTransactionRows.ToString(CultureInfo.InvariantCulture), row.BankTransactionDeposit.ToString("0.00", CultureInfo.InvariantCulture), row.BankTransactionWithdraw.ToString("0.00", CultureInfo.InvariantCulture), row.StatementRows.ToString(CultureInfo.InvariantCulture), row.StatementCredit.ToString("0.00", CultureInfo.InvariantCulture), row.StatementDebit.ToString("0.00", CultureInfo.InvariantCulture), row.UnreconciledBankTransactions.ToString(CultureInfo.InvariantCulture), row.UnmatchedStatementLines.ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine();
        builder.AppendLine("Issue,Severity,Code,Source Type,Source Id,Message,Action Path");
        foreach (var issue in report.Issues)
        {
            AppendCsvRow(builder, "Issue", issue.Severity, issue.Code, issue.SourceType, issue.SourceId?.ToString("D") ?? string.Empty, issue.Message, issue.ActionPath);
        }

        builder.AppendLine();
        builder.AppendLine("Settlement,Direction,Source Type,Source Number,Date,Party,Mode,Amount,Bank Account,Reference,Adjustment Source,Has Bank,Has Reference,Matched,Reconciled,Notes,Action Path");
        foreach (var row in report.SettlementRows)
        {
            AppendCsvRow(builder, "Settlement", row.Direction, row.SourceType, row.SourceNumber, row.OnDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.PartyName, row.PaymentMode, row.Amount.ToString("0.00", CultureInfo.InvariantCulture), row.BankAccountName, row.ReferenceNumber, row.AdjustmentSourceType, row.HasBankAccount ? "Yes" : "No", row.HasReference ? "Yes" : "No", row.MatchedBankTransaction ? "Yes" : "No", row.Reconciled ? "Yes" : "No", row.Notes, row.ActionPath);
        }

        return builder.ToString();
    }

    private static void AppendCsvRow(StringBuilder builder, params string?[] values)
    {
        builder.AppendLine(string.Join(',', values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}

public sealed record BankReconciliationClosureReportDto(
    string Status,
    DateTime From,
    DateTime To,
    int CriticalIssues,
    int WarningIssues,
    IReadOnlyList<BankReconciliationMetricDto> Metrics,
    IReadOnlyList<BankReconciliationModeSummaryDto> PaymentModeSummary,
    IReadOnlyList<BankAccountReconciliationRowDto> BankAccounts,
    IReadOnlyList<BankSettlementEvidenceRowDto> SettlementRows,
    IReadOnlyList<BankReconciliationIssueDto> Issues,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);

public sealed record BankReconciliationMetricDto(string Label, int? Count, decimal? Amount, string Description);
public sealed record BankReconciliationModeSummaryDto(string PaymentMode, int Count, decimal Amount, int MissingBankAccount, int MissingReference, int Matched, int Reconciled);
public sealed record BankAccountReconciliationRowDto(Guid BankAccountId, string AccountHolderName, string MaskedAccountNumber, bool Active, int SettlementRows, decimal SettlementAmount, int BankTransactionRows, decimal BankTransactionDeposit, decimal BankTransactionWithdraw, int StatementRows, decimal StatementCredit, decimal StatementDebit, int UnreconciledBankTransactions, int UnmatchedStatementLines);
public sealed record BankSettlementEvidenceRowDto(Guid SourceId, string Direction, string SourceType, string SourceNumber, DateTime OnDate, string PartyName, string PaymentMode, decimal Amount, Guid? BankAccountId, string BankAccountName, string ReferenceNumber, string AdjustmentSourceType, bool HasBankAccount, bool HasReference, bool MatchedBankTransaction, bool Reconciled, string Notes, string ActionPath);
public sealed record BankReconciliationIssueDto(string Severity, string Code, string SourceType, Guid? SourceId, string Message, string ActionPath);
