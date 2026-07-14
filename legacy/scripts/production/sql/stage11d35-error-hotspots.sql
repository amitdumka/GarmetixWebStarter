\echo 'Garmetix Stage 11D-35 database hotspot report'
\echo 'Companies / stores'
SELECT c."Name" AS company, s."Name" AS store, s."StoreCode" FROM "Companies" c LEFT JOIN "Stores" s ON s."CompanyId" = c."Id" ORDER BY c."Name", s."Name";
\echo 'Invoice count summary'
SELECT 'PurchaseInvoices' AS table_name, count(*) AS rows FROM "PurchaseInvoices"
UNION ALL SELECT 'SalesInvoices', count(*) FROM "SalesInvoices"
UNION ALL SELECT 'InvoiceItems', count(*) FROM "InvoiceItems"
UNION ALL SELECT 'Stocks', count(*) FROM "Stocks";
\echo 'Recent purchase invoices with suspicious totals'
SELECT "InvoiceNumber", "InwardNumber", "BillAmount", "TaxAmount", "FrightAmount", "OnDate" FROM "PurchaseInvoices" WHERE COALESCE("BillAmount",0) <= 0 OR COALESCE("TaxAmount",0) < 0 ORDER BY "OnDate" DESC LIMIT 50;
\echo 'Recent stock with non-positive quantity or cost'
SELECT "Barcode", "PurchaseQty", "CostPrice", "MRP", "UpdatedAt" FROM "Stocks" WHERE COALESCE("PurchaseQty",0) < 0 OR COALESCE("CostPrice",0) < 0 ORDER BY "UpdatedAt" DESC LIMIT 100;
\echo 'Attendance rows requiring review'
SELECT "OnDate", "EmployeeId", "AttendanceStatus", "CheckInTime", "CheckOutTime", "Remarks" FROM "Attendance" WHERE COALESCE("Remarks",'') ILIKE '%review%' ORDER BY "OnDate" DESC LIMIT 100;
