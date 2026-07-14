\echo 'Stage 11D-101 Purchase/Vendor Payment Runtime QA DB check started'
\echo '1) Schema check: PurchaseInvoices.InwardDate and PurchasePayments maintenance columns'
SELECT
  CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='PurchaseInvoices' AND column_name='InwardDate')
       THEN 'OK: PurchaseInvoices.InwardDate exists' ELSE 'FAIL: PurchaseInvoices.InwardDate missing' END AS inward_date_column_check,
  CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='PurchasePayments' AND column_name='VoucherId')
       THEN 'OK: PurchasePayments.VoucherId exists' ELSE 'WARN: PurchasePayments.VoucherId missing' END AS payment_voucher_column_check,
  CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='PurchasePayments' AND column_name='Deleted')
       THEN 'OK: PurchasePayments.Deleted exists' ELSE 'FAIL: PurchasePayments.Deleted missing' END AS payment_deleted_column_check;

\echo '2) Recent purchase inwards with invoice date and inward date'
SELECT
  "InwardNumber",
  "InvoiceNumber",
  "OnDate"::date AS supplier_invoice_date,
  "InwardDate"::date AS inward_date,
  "BillAmount",
  "InvoiceStatus",
  "CreatedAt"
FROM "PurchaseInvoices"
WHERE COALESCE("Deleted", false) = false
ORDER BY "CreatedAt" DESC
LIMIT 25;

\echo '3) Purchase stock movement date mismatch. Result should be empty unless old data uses earlier logic.'
SELECT
  pi."InwardNumber",
  pi."InvoiceNumber",
  pi."InwardDate"::date AS inward_date,
  sm."OnDate"::date AS movement_date,
  sm."Barcode",
  sm."QuantityIn",
  sm."SourceNumber"
FROM "PurchaseInvoices" pi
JOIN "StockMovements" sm ON sm."SourceId" = pi."Id" AND sm."SourceType" = 'PurchaseInvoice'
WHERE COALESCE(pi."Deleted", false) = false
  AND sm."QuantityIn" > 0
  AND sm."OnDate"::date <> pi."InwardDate"::date
ORDER BY pi."CreatedAt" DESC, sm."Barcode"
LIMIT 100;

\echo '4) Recent vendor payments with linked invoice/voucher status'
SELECT
  pp."OnDate"::date AS payment_date,
  pp."Amount",
  pp."PaymentMode",
  pp."BankAccountId",
  pp."ReferenceNumber",
  pp."Deleted" AS payment_deleted,
  pi."InvoiceNumber" AS purchase_invoice_number,
  pi."InwardNumber",
  pi."BillAmount",
  pi."InvoiceStatus",
  v."Name" AS vendor_name,
  vo."VoucherNumber",
  vo."Deleted" AS voucher_deleted
FROM "PurchasePayments" pp
LEFT JOIN "PurchaseInvoices" pi ON pi."Id" = pp."PurchaseInvoiceId"
LEFT JOIN "Vendors" v ON v."Id" = pp."VendorId"
LEFT JOIN "Vouchers" vo ON vo."Id" = pp."VoucherId"
ORDER BY pp."CreatedAt" DESC
LIMIT 50;

\echo '5) Active purchase invoices where paid sum and status disagree. Review result.'
WITH paid AS (
  SELECT "PurchaseInvoiceId", COALESCE(SUM("Amount"), 0) AS paid_amount
  FROM "PurchasePayments"
  WHERE COALESCE("Deleted", false) = false
  GROUP BY "PurchaseInvoiceId"
)
SELECT
  pi."InvoiceNumber",
  pi."InwardNumber",
  pi."BillAmount",
  COALESCE(paid.paid_amount, 0) AS active_payment_total,
  pi."InvoiceStatus",
  ROUND((pi."BillAmount" - COALESCE(paid.paid_amount, 0))::numeric, 2) AS balance_by_payments
FROM "PurchaseInvoices" pi
LEFT JOIN paid ON paid."PurchaseInvoiceId" = pi."Id"
WHERE COALESCE(pi."Deleted", false) = false
  AND (
    (COALESCE(paid.paid_amount, 0) <= 0 AND pi."InvoiceStatus" NOT IN (0, 1))
    OR (COALESCE(paid.paid_amount, 0) > 0 AND COALESCE(paid.paid_amount, 0) < pi."BillAmount" AND pi."InvoiceStatus" NOT IN (2, 3))
    OR (COALESCE(paid.paid_amount, 0) >= pi."BillAmount" AND pi."InvoiceStatus" NOT IN (3, 4))
  )
ORDER BY pi."CreatedAt" DESC
LIMIT 100;

\echo '6) Deleted vendor payments that still have active linked voucher/bank/journal entries. Review result.'
SELECT
  pp."Id" AS payment_id,
  pp."Amount",
  pp."OnDate"::date AS payment_date,
  pp."VoucherId",
  vo."VoucherNumber",
  vo."Deleted" AS voucher_deleted,
  COUNT(DISTINCT bt."Id") FILTER (WHERE COALESCE(bt."Deleted", false) = false) AS active_bank_transactions,
  COUNT(DISTINCT je."Id") FILTER (WHERE COALESCE(je."Deleted", false) = false) AS active_journals
FROM "PurchasePayments" pp
LEFT JOIN "Vouchers" vo ON vo."Id" = pp."VoucherId"
LEFT JOIN "BankTransactions" bt ON bt."Reference" = vo."VoucherNumber"
LEFT JOIN "JournalEntries" je ON je."SourceType" = 'VendorPaymentVoucher' AND je."SourceId" = vo."Id"
WHERE COALESCE(pp."Deleted", false) = true
GROUP BY pp."Id", pp."Amount", pp."OnDate", pp."VoucherId", vo."VoucherNumber", vo."Deleted"
HAVING COALESCE(vo."Deleted", true) = false
   OR COUNT(DISTINCT bt."Id") FILTER (WHERE COALESCE(bt."Deleted", false) = false) > 0
   OR COUNT(DISTINCT je."Id") FILTER (WHERE COALESCE(je."Deleted", false) = false) > 0
ORDER BY pp."OnDate" DESC
LIMIT 100;

\echo '7) Non-cash active vendor payments without bank account. Result should be empty.'
SELECT
  pp."Id",
  pp."OnDate"::date AS payment_date,
  pp."Amount",
  pp."PaymentMode",
  pp."BankAccountId",
  pp."ReferenceNumber",
  pi."InvoiceNumber",
  v."Name" AS vendor_name
FROM "PurchasePayments" pp
LEFT JOIN "PurchaseInvoices" pi ON pi."Id" = pp."PurchaseInvoiceId"
LEFT JOIN "Vendors" v ON v."Id" = pp."VendorId"
WHERE COALESCE(pp."Deleted", false) = false
  AND pp."PaymentMode" <> 0
  AND pp."BankAccountId" IS NULL
ORDER BY pp."CreatedAt" DESC
LIMIT 100;

\echo '8) Purchase/vendor payment daily totals for recent activity'
SELECT
  pp."OnDate"::date AS payment_date,
  COUNT(*) FILTER (WHERE COALESCE(pp."Deleted", false) = false) AS active_payment_count,
  COALESCE(SUM(pp."Amount") FILTER (WHERE COALESCE(pp."Deleted", false) = false), 0) AS active_payment_total,
  COUNT(*) FILTER (WHERE COALESCE(pp."Deleted", false) = true) AS deleted_payment_count,
  COALESCE(SUM(pp."Amount") FILTER (WHERE COALESCE(pp."Deleted", false) = true), 0) AS deleted_payment_total
FROM "PurchasePayments" pp
GROUP BY pp."OnDate"::date
ORDER BY payment_date DESC
LIMIT 30;

\echo 'Stage 11D-101 Purchase/Vendor Payment Runtime QA DB check completed'
