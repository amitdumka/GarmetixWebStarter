\pset pager off
\echo 'Recent invoice correction audit rows'
SELECT
    "CreatedAt",
    "TableName",
    "Operation",
    COALESCE("InvoiceNumber", "InvoiceId"::text) AS invoice,
    "SourceType",
    "ChangedBy"
FROM "InvoiceCorrectionAudits"
ORDER BY "CreatedAt" DESC
LIMIT 200;
