\echo 'Stage 11D-103 purchase/vendor payment reconciliation check'

\echo '1) Vendor paid mismatch against active purchase payments'
SELECT
  v."Name" AS vendor,
  v."Paid" AS vendor_paid,
  COALESCE(SUM(pp."Amount") FILTER (WHERE pp."Deleted" = false), 0) AS active_purchase_payment_total,
  v."Paid" - COALESCE(SUM(pp."Amount") FILTER (WHERE pp."Deleted" = false), 0) AS difference
FROM "Vendors" v
LEFT JOIN "PurchasePayments" pp ON pp."VendorId" = v."Id"
GROUP BY v."Id", v."Name", v."Paid"
HAVING ABS(v."Paid" - COALESCE(SUM(pp."Amount") FILTER (WHERE pp."Deleted" = false), 0)) > 0.01
ORDER BY ABS(v."Paid" - COALESCE(SUM(pp."Amount") FILTER (WHERE pp."Deleted" = false), 0)) DESC
LIMIT 50;

\echo '2) Purchase invoice payment/status mismatch'
WITH paid AS (
  SELECT "PurchaseInvoiceId", SUM("Amount") AS paid_amount
  FROM "PurchasePayments"
  WHERE "Deleted" = false AND "PurchaseInvoiceId" <> '00000000-0000-0000-0000-000000000000'
  GROUP BY "PurchaseInvoiceId"
)
SELECT
  pi."InvoiceNumber",
  pi."InwardNumber",
  pi."BillAmount",
  COALESCE(paid.paid_amount, 0) AS active_paid,
  pi."InvoiceStatus",
  CASE
    WHEN COALESCE(paid.paid_amount, 0) <= 0 THEN 'Pending'
    WHEN COALESCE(paid.paid_amount, 0) >= pi."BillAmount" THEN 'Paid'
    ELSE 'PartiallyPaid'
  END AS expected_status
FROM "PurchaseInvoices" pi
LEFT JOIN paid ON paid."PurchaseInvoiceId" = pi."Id"
WHERE (
  CASE
    WHEN COALESCE(paid.paid_amount, 0) <= 0 THEN 0
    WHEN COALESCE(paid.paid_amount, 0) >= pi."BillAmount" THEN 1
    ELSE 2
  END
) <> pi."InvoiceStatus"
ORDER BY pi."CreatedAt" DESC
LIMIT 50;

\echo '3) Active bank transaction rows for deleted vendor vouchers'
SELECT
  v."VoucherNumber",
  v."Deleted" AS voucher_deleted,
  bt."Reference",
  bt."Amount",
  bt."Deleted" AS bank_transaction_deleted
FROM "Vouchers" v
JOIN "BankTransactions" bt ON bt."Reference" = v."VoucherNumber"
WHERE v."Deleted" = true AND bt."Deleted" = false
ORDER BY bt."UpdatedAt" DESC NULLS LAST, bt."OnDate" DESC
LIMIT 50;

\echo '4) Recent vendor payments'
SELECT
  pp."OnDate",
  vd."Name" AS vendor,
  pi."InvoiceNumber",
  pp."Amount",
  pp."PaymentMode",
  pp."BankAccountId",
  pp."ReferenceNumber",
  pp."Deleted"
FROM "PurchasePayments" pp
LEFT JOIN "Vendors" vd ON vd."Id" = pp."VendorId"
LEFT JOIN "PurchaseInvoices" pi ON pi."Id" = pp."PurchaseInvoiceId"
ORDER BY pp."UpdatedAt" DESC NULLS LAST, pp."CreatedAt" DESC
LIMIT 25;
