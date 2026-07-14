-- Stage 11D-94 / v4.12.09
-- Runtime repair for databases where the code has SalesInvoices.Remarks but the deployed
-- PostgreSQL volume missed the migration because AutoMigrate was disabled or migration history drifted.
\echo 'Ensuring SalesInvoices.Remarks exists...'
ALTER TABLE "SalesInvoices" ADD COLUMN IF NOT EXISTS "Remarks" text NULL;

\echo 'SalesInvoices.Remarks status:'
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'SalesInvoices'
  AND column_name = 'Remarks';

\echo 'Recent Vyapar imported invoice marker count:'
SELECT COUNT(*) AS vyapar_imported_invoices
FROM "SalesInvoices"
WHERE COALESCE("Remarks", '') LIKE '%VyaparSourceInvoice=%';
