\echo 'Stage 11D-91 Vyapar Sale Import DB check started'
\echo '1) Schema check: SalesInvoices.Remarks must exist'
SELECT
  CASE WHEN EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_name = 'SalesInvoices'
      AND column_name = 'Remarks'
  ) THEN 'OK: SalesInvoices.Remarks exists'
  ELSE 'FAIL: SalesInvoices.Remarks missing. Run EF migration/AddSaleInvoiceRemarks before importing.' END AS remarks_column_check;

\echo '2) Imported Vyapar invoice totals'
SELECT
  COUNT(*) AS imported_invoice_count,
  COALESCE(SUM("BillAmount"), 0) AS bill_amount_total,
  COALESCE(SUM("PaidAmount"), 0) AS paid_amount_total,
  COALESCE(SUM("BillAmount" - "PaidAmount"), 0) AS balance_amount_total,
  MIN("OnDate") AS first_invoice_date,
  MAX("OnDate") AS last_invoice_date
FROM "SalesInvoices"
WHERE "Remarks" ILIKE '%VyaparSaleImport%';

\echo '3) Duplicate Vyapar source invoice/date mapping. Result should be empty.'
WITH imported AS (
  SELECT
    "Id",
    "InvoiceNumber",
    "OnDate"::date AS invoice_date,
    btrim(substring("Remarks" FROM 'VyaparSourceInvoice=([^|]+)')) AS source_invoice,
    btrim(substring("Remarks" FROM 'VyaparInvoiceDate=([^|]+)')) AS source_date_text
  FROM "SalesInvoices"
  WHERE "Remarks" ILIKE '%VyaparSaleImport%'
), keys AS (
  SELECT
    COALESCE(NULLIF(source_invoice, ''), "InvoiceNumber") AS source_invoice,
    COALESCE(NULLIF(source_date_text, '')::date, invoice_date) AS source_date,
    COUNT(*) AS invoice_count,
    string_agg("InvoiceNumber", ', ' ORDER BY "InvoiceNumber") AS garmetix_invoice_numbers
  FROM imported
  GROUP BY COALESCE(NULLIF(source_invoice, ''), "InvoiceNumber"), COALESCE(NULLIF(source_date_text, '')::date, invoice_date)
)
SELECT *
FROM keys
WHERE invoice_count > 1
ORDER BY source_date, source_invoice;

\echo '4) Imported invoices without items. Result should be empty.'
SELECT
  si."InvoiceNumber",
  si."OnDate",
  si."BillAmount",
  si."Remarks"
FROM "SalesInvoices" si
LEFT JOIN "InvoiceItems" ii ON ii."InvoiceId" = si."Id"
WHERE si."Remarks" ILIKE '%VyaparSaleImport%'
GROUP BY si."Id", si."InvoiceNumber", si."OnDate", si."BillAmount", si."Remarks"
HAVING COUNT(ii."Id") = 0
ORDER BY si."OnDate", si."InvoiceNumber";

\echo '5) Imported invoice paid amount vs payment rows. Result should be empty or reviewed.'
SELECT
  si."InvoiceNumber",
  si."OnDate",
  si."PaidAmount" AS invoice_paid_amount,
  COALESCE(SUM(ip."Amount"), 0) AS payment_row_total,
  ROUND((si."PaidAmount" - COALESCE(SUM(ip."Amount"), 0))::numeric, 2) AS difference
FROM "SalesInvoices" si
LEFT JOIN "InvoicePayments" ip ON ip."InvoiceId" = si."Id"
WHERE si."Remarks" ILIKE '%VyaparSaleImport%'
GROUP BY si."Id", si."InvoiceNumber", si."OnDate", si."PaidAmount"
HAVING ABS(si."PaidAmount" - COALESCE(SUM(ip."Amount"), 0)) > 0.50
ORDER BY si."OnDate", si."InvoiceNumber";

\echo '6) Non-cash imported payment without bank/reference/details. Review these.'
SELECT
  si."InvoiceNumber",
  si."OnDate",
  ip."Amount",
  ip."PaymentMode",
  ip."BankAccountId",
  ip."ReferenceNumber",
  ip."PaymentDetailsJson"
FROM "SalesInvoices" si
JOIN "InvoicePayments" ip ON ip."InvoiceId" = si."Id"
WHERE si."Remarks" ILIKE '%VyaparSaleImport%'
  AND ip."PaymentMode" <> 0
  AND (ip."BankAccountId" IS NULL OR NULLIF(ip."ReferenceNumber", '') IS NULL OR NULLIF(ip."PaymentDetailsJson", '') IS NULL)
ORDER BY si."OnDate", si."InvoiceNumber";

\echo '7) Imported invoices with stock out movement count lower than item count. Review these.'
WITH item_counts AS (
  SELECT "InvoiceId", COUNT(*) AS item_count
  FROM "InvoiceItems"
  GROUP BY "InvoiceId"
), movement_counts AS (
  SELECT "SourceId", COUNT(*) AS movement_count
  FROM "StockMovements"
  WHERE "SourceType" = 'VyaparSaleImport'
  GROUP BY "SourceId"
)
SELECT
  si."InvoiceNumber",
  si."OnDate",
  COALESCE(ic.item_count, 0) AS item_count,
  COALESCE(mc.movement_count, 0) AS movement_count
FROM "SalesInvoices" si
LEFT JOIN item_counts ic ON ic."InvoiceId" = si."Id"
LEFT JOIN movement_counts mc ON mc."SourceId" = si."Id"
WHERE si."Remarks" ILIKE '%VyaparSaleImport%'
  AND COALESCE(mc.movement_count, 0) < COALESCE(ic.item_count, 0)
ORDER BY si."OnDate", si."InvoiceNumber";

\echo '8) Imported invoices by date summary'
SELECT
  "OnDate"::date AS invoice_date,
  COUNT(*) AS invoice_count,
  COALESCE(SUM("BillAmount"), 0) AS bill_amount,
  COALESCE(SUM("PaidAmount"), 0) AS paid_amount
FROM "SalesInvoices"
WHERE "Remarks" ILIKE '%VyaparSaleImport%'
GROUP BY "OnDate"::date
ORDER BY invoice_date;

\echo 'Stage 11D-91 Vyapar Sale Import DB check completed'
