-- Stage 11D-46 non-mutating runtime checks for sale payment integrity.
-- Run inside Postgres using: psql -U garmetix -d garmetix -f this-file

\echo '1) Sales invoice paid amount mismatch count'
WITH payment_totals AS (
    SELECT "InvoiceId", ROUND(COALESCE(SUM("Amount"), 0), 2) AS "PaidFromRows"
    FROM "InvoicePayments"
    GROUP BY "InvoiceId"
)
SELECT COUNT(*) AS mismatch_count
FROM "SalesInvoices" invoice
LEFT JOIN payment_totals payments ON payments."InvoiceId" = invoice."Id"
WHERE ROUND(invoice."PaidAmount", 2) <> ROUND(COALESCE(payments."PaidFromRows", 0), 2);

\echo '2) Recent mixed-payment sale allocation review. PaymentMode: 0=Cash, 2=UPI, 12=MixPayments'
SELECT
    invoice."InvoiceNumber",
    invoice."OnDate"::date AS sale_date,
    ROUND(invoice."BillAmount", 2) AS bill_amount,
    ROUND(invoice."PaidAmount", 2) AS paid_amount,
    ROUND(SUM(CASE WHEN payment."PaymentMode" = 0 THEN payment."Amount" ELSE 0 END), 2) AS cash_amount,
    ROUND(SUM(CASE WHEN payment."PaymentMode" <> 0 THEN payment."Amount" ELSE 0 END), 2) AS non_cash_amount,
    COUNT(payment."Id") AS payment_rows
FROM "SalesInvoices" invoice
JOIN "InvoicePayments" payment ON payment."InvoiceId" = invoice."Id"
WHERE invoice."OnDate" >= (CURRENT_DATE - INTERVAL '30 days')
GROUP BY invoice."InvoiceNumber", invoice."OnDate", invoice."BillAmount", invoice."PaidAmount"
HAVING COUNT(payment."Id") > 1
ORDER BY invoice."OnDate" DESC, invoice."InvoiceNumber" DESC
LIMIT 25;

\echo '3) Recent cancelled invoices that are eligible candidates for admin hard delete review'
SELECT
    invoice."InvoiceNumber",
    invoice."OnDate"::date AS sale_date,
    ROUND(invoice."BillAmount", 2) AS bill_amount,
    invoice."InvoiceStatus"
FROM "SalesInvoices" invoice
WHERE invoice."InvoiceStatus" = 5
ORDER BY invoice."OnDate" DESC, invoice."InvoiceNumber" DESC
LIMIT 25;
