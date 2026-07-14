\pset pager off
\pset border 2
\pset null '∅'
\echo 'Garmetix Production Data Validation - Aadwika Fashion / Smart Menswear'
\echo '================================================================================'

DROP TABLE IF EXISTS _garmetix_validation_context;
CREATE TEMP TABLE _garmetix_validation_context AS
SELECT c."Id" AS company_id, s."Id" AS store_id, s."StoreGroupId" AS store_group_id, c."Name" AS company_name, s."Name" AS store_name
FROM "Companies" c
JOIN "Stores" s ON s."CompanyId" = c."Id"
WHERE lower(c."Name") = lower(:'company_name')
  AND lower(s."Name") = lower(:'store_name')
ORDER BY s."CreatedAt" NULLS LAST
LIMIT 1;

\echo ''
\echo '1) Context check'
SELECT
  CASE WHEN count(*) = 1 THEN 'PASS' ELSE 'FAIL' END AS status,
  COALESCE(max(company_name), :'company_name') AS company,
  COALESCE(max(store_name), :'store_name') AS store,
  count(*) AS matched_rows
FROM _garmetix_validation_context;

\echo ''
\echo '2) Master data counts'
SELECT 'Vendors' AS metric, count(*) AS count FROM "Vendors" v JOIN _garmetix_validation_context x ON x.company_id = v."CompanyId"
UNION ALL SELECT 'Brands', count(*) FROM "Brands" b
UNION ALL SELECT 'Product categories', count(*) FROM "ProductCategories" pc JOIN _garmetix_validation_context x ON x.company_id = pc."CompanyId"
UNION ALL SELECT 'Product sub-categories', count(*) FROM "ProductSubCategories" psc JOIN _garmetix_validation_context x ON x.company_id = psc."CompanyId"
UNION ALL SELECT 'Products', count(*) FROM "Products" p JOIN _garmetix_validation_context x ON x.company_id = p."CompanyId"
UNION ALL SELECT 'Product details', count(*) FROM "ProductDetails" pd
UNION ALL SELECT 'Stocks', count(*) FROM "Stocks" st JOIN _garmetix_validation_context x ON x.company_id = st."CompanyId" AND x.store_id = st."StoreId";

\echo ''
\echo '3) Purchase import summary'
SELECT 'Purchase invoices' AS metric, count(*) AS count, COALESCE(sum(pi."Quantity"),0) AS qty, COALESCE(sum(pi."BillAmount"),0) AS amount
FROM "PurchaseInvoices" pi JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
UNION ALL
SELECT 'Purchase invoice items', count(*), COALESCE(sum(ii."BilledQuantity"),0), COALESCE(sum(ii."Amount"),0)
FROM "InvoiceItems" ii
JOIN "PurchaseInvoices" pi ON pi."Id" = ii."InvoiceId"
JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
WHERE COALESCE(ii."Discriminator", '') = 'PurchaseInvoiceItem'
UNION ALL
SELECT 'Purchase stock movements', count(*), COALESCE(sum(sm."QuantityIn"),0), COALESCE(sum(sm."CostImpact"),0)
FROM "StockMovements" sm JOIN _garmetix_validation_context x ON x.company_id = sm."CompanyId" AND x.store_id = sm."StoreId"
WHERE sm."SourceType" = 'PurchaseInvoice';

\echo ''
\echo '4) Inward number validation - should be SM/YYYYMM/INW/0001'
WITH inward AS (
  SELECT pi."InwardNumber", count(*) AS row_count, min(pi."OnDate") AS first_invoice_date, max(pi."InwardDate") AS last_inward_date
  FROM "PurchaseInvoices" pi JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
  GROUP BY pi."InwardNumber"
)
SELECT
  CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'FAIL' END AS status,
  count(*) AS invalid_inward_count
FROM inward
WHERE COALESCE("InwardNumber", '') !~ '^SM/[0-9]{6}/INW/[0-9]{4}$';

SELECT "InwardNumber", row_count, first_invoice_date, last_inward_date
FROM (
  SELECT pi."InwardNumber", count(*) AS row_count, min(pi."OnDate") AS first_invoice_date, max(pi."InwardDate") AS last_inward_date
  FROM "PurchaseInvoices" pi JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
  GROUP BY pi."InwardNumber"
) t
WHERE COALESCE("InwardNumber", '') !~ '^SM/[0-9]{6}/INW/[0-9]{4}$'
ORDER BY "InwardNumber"
LIMIT 50;

\echo ''
\echo '5) Duplicate purchase invoice/inward checks'
SELECT
  CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'FAIL' END AS status,
  count(*) AS duplicate_inward_groups
FROM (
  SELECT pi."InwardNumber"
  FROM "PurchaseInvoices" pi JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
  GROUP BY pi."InwardNumber"
  HAVING count(*) > 1
) d;

SELECT pi."InwardNumber", count(*) AS duplicate_count, string_agg(pi."InvoiceNumber", ', ' ORDER BY pi."InvoiceNumber") AS invoices
FROM "PurchaseInvoices" pi JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
GROUP BY pi."InwardNumber"
HAVING count(*) > 1
ORDER BY pi."InwardNumber"
LIMIT 50;

\echo ''
\echo '6) Purchase item discount / amount sanity'
SELECT
  CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'FAIL' END AS status,
  count(*) AS suspicious_discount_rows
FROM "InvoiceItems" ii
JOIN "PurchaseInvoices" pi ON pi."Id" = ii."InvoiceId"
JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
WHERE COALESCE(ii."Discriminator", '') = 'PurchaseInvoiceItem'
  AND (COALESCE(ii."DiscountAmount",0) < 0 OR COALESCE(ii."DiscountAmount",0) > COALESCE(ii."BasePrice",0) * COALESCE(ii."BilledQuantity",0));

SELECT pi."InvoiceNumber", pi."InwardNumber", ii."ProductName", ii."BilledQuantity", ii."BasePrice", ii."DiscountAmount", ii."TaxPercentage", ii."TaxAmount", ii."Amount"
FROM "InvoiceItems" ii
JOIN "PurchaseInvoices" pi ON pi."Id" = ii."InvoiceId"
JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
WHERE COALESCE(ii."Discriminator", '') = 'PurchaseInvoiceItem'
  AND (COALESCE(ii."DiscountAmount",0) < 0 OR COALESCE(ii."DiscountAmount",0) > COALESCE(ii."BasePrice",0) * COALESCE(ii."BilledQuantity",0))
ORDER BY pi."InvoiceNumber", ii."ProductName"
LIMIT 50;

\echo ''
\echo '7) Purchase invoice totals vs item totals'
WITH item_sums AS (
  SELECT pi."Id", pi."InvoiceNumber", pi."InwardNumber", pi."BillAmount", pi."FrightAmount", pi."RoundOff",
         COALESCE(sum(ii."Amount"),0) AS item_amount,
         COALESCE(sum(ii."TaxAmount"),0) AS item_tax,
         COALESCE(sum(ii."BilledQuantity"),0) AS item_qty
  FROM "PurchaseInvoices" pi
  JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
  LEFT JOIN "InvoiceItems" ii ON ii."InvoiceId" = pi."Id" AND COALESCE(ii."Discriminator", '') = 'PurchaseInvoiceItem'
  GROUP BY pi."Id", pi."InvoiceNumber", pi."InwardNumber", pi."BillAmount", pi."FrightAmount", pi."RoundOff"
)
SELECT
  CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END AS status,
  count(*) AS invoices_with_total_difference_gt_2_rupees
FROM item_sums
WHERE abs(COALESCE("BillAmount",0) - (COALESCE(item_amount,0) + COALESCE("FrightAmount",0) + COALESCE("RoundOff",0))) > 2;

WITH item_sums AS (
  SELECT pi."Id", pi."InvoiceNumber", pi."InwardNumber", pi."BillAmount", pi."FrightAmount", pi."RoundOff",
         COALESCE(sum(ii."Amount"),0) AS item_amount
  FROM "PurchaseInvoices" pi
  JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
  LEFT JOIN "InvoiceItems" ii ON ii."InvoiceId" = pi."Id" AND COALESCE(ii."Discriminator", '') = 'PurchaseInvoiceItem'
  GROUP BY pi."Id", pi."InvoiceNumber", pi."InwardNumber", pi."BillAmount", pi."FrightAmount", pi."RoundOff"
)
SELECT "InvoiceNumber", "InwardNumber", "BillAmount", item_amount, "FrightAmount", "RoundOff",
       round((COALESCE("BillAmount",0) - (COALESCE(item_amount,0) + COALESCE("FrightAmount",0) + COALESCE("RoundOff",0)))::numeric,2) AS difference
FROM item_sums
WHERE abs(COALESCE("BillAmount",0) - (COALESCE(item_amount,0) + COALESCE("FrightAmount",0) + COALESCE("RoundOff",0))) > 2
ORDER BY abs(COALESCE("BillAmount",0) - (COALESCE(item_amount,0) + COALESCE("FrightAmount",0) + COALESCE("RoundOff",0))) DESC
LIMIT 50;

\echo ''
\echo '8) Stock validation'
SELECT
  CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'FAIL' END AS status,
  count(*) AS zero_or_negative_stock_rows
FROM "Stocks" s JOIN _garmetix_validation_context x ON x.company_id = s."CompanyId" AND x.store_id = s."StoreId"
WHERE COALESCE(s."PurchaseQty",0) <= 0;

SELECT s."Barcode", p."Name" AS product_name, s."PurchaseQty", s."SoldQty", s."CostPrice", s."MRP"
FROM "Stocks" s
JOIN _garmetix_validation_context x ON x.company_id = s."CompanyId" AND x.store_id = s."StoreId"
LEFT JOIN "Products" p ON p."Id" = s."ProductId"
WHERE COALESCE(s."PurchaseQty",0) <= 0
ORDER BY s."Barcode"
LIMIT 50;

SELECT
  CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'FAIL' END AS status,
  count(*) AS purchase_movements_with_zero_qty
FROM "StockMovements" sm JOIN _garmetix_validation_context x ON x.company_id = sm."CompanyId" AND x.store_id = sm."StoreId"
WHERE sm."SourceType" = 'PurchaseInvoice' AND COALESCE(sm."QuantityIn",0) <= 0;

\echo ''
\echo '9) Product master quality'
SELECT 'Products missing HSN' AS issue, count(*) AS count FROM "Products" p JOIN _garmetix_validation_context x ON x.company_id = p."CompanyId" WHERE COALESCE(trim(p."HSNCode"),'') = ''
UNION ALL SELECT 'Products missing category', count(*) FROM "Products" p JOIN _garmetix_validation_context x ON x.company_id = p."CompanyId" WHERE p."ProductCategoryId" IS NULL
UNION ALL SELECT 'Products missing sub-category', count(*) FROM "Products" p JOIN _garmetix_validation_context x ON x.company_id = p."CompanyId" WHERE p."ProductSubCategoryId" IS NULL
UNION ALL SELECT 'Product details missing brand', count(*) FROM "ProductDetails" pd WHERE COALESCE(trim(pd."Brand"),'') = ''
UNION ALL SELECT 'Products without stock row in Smart Menswear', count(*) FROM "Products" p JOIN _garmetix_validation_context x ON x.company_id = p."CompanyId" WHERE NOT EXISTS (SELECT 1 FROM "Stocks" s WHERE s."CompanyId" = x.company_id AND s."StoreId" = x.store_id AND s."ProductId" = p."Id");

\echo ''
\echo '10) Vendor ledger summary vs purchase invoice totals'
WITH pi AS (
  SELECT pi."VendorId", count(*) AS invoice_count, COALESCE(sum(pi."BillAmount"),0) AS invoice_amount
  FROM "PurchaseInvoices" pi JOIN _garmetix_validation_context x ON x.company_id = pi."CompanyId" AND x.store_id = pi."StoreId"
  GROUP BY pi."VendorId"
)
SELECT
  v."Name" AS vendor,
  v."BillCount" AS vendor_bill_count,
  COALESCE(pi.invoice_count,0) AS invoice_count,
  v."BillAmount" AS vendor_bill_amount,
  COALESCE(pi.invoice_amount,0) AS invoice_amount,
  round((COALESCE(v."BillAmount",0) - COALESCE(pi.invoice_amount,0))::numeric,2) AS amount_difference,
  v."Paid",
  round((COALESCE(v."BillAmount",0)-COALESCE(v."Paid",0))::numeric,2) AS balance
FROM "Vendors" v
JOIN _garmetix_validation_context x ON x.company_id = v."CompanyId"
LEFT JOIN pi ON pi."VendorId" = v."Id"
WHERE COALESCE(pi.invoice_count,0) > 0 OR COALESCE(v."BillAmount",0) <> 0
ORDER BY abs(COALESCE(v."BillAmount",0) - COALESCE(pi.invoice_amount,0)) DESC, v."Name"
LIMIT 100;

\echo ''
\echo '11) Attendance / shift readiness'
SELECT 'Active employees' AS metric, count(*) AS count
FROM "Employees" e JOIN _garmetix_validation_context x ON x.company_id = e."CompanyId" AND x.store_id = e."StoreId"
WHERE COALESCE(e."Working", false) = true AND COALESCE(e."EmployeeStatus", 'Active') NOT IN ('Resigned','Terminated','Inactive')
UNION ALL SELECT 'Attendance shifts', count(*) FROM "AttendanceShifts" sh JOIN _garmetix_validation_context x ON x.company_id = sh."CompanyId" AND x.store_id = sh."StoreId"
UNION ALL SELECT 'Employee shift rules', count(*) FROM "EmployeeAttendanceShiftRules" r JOIN _garmetix_validation_context x ON x.company_id = r."CompanyId" AND x.store_id = r."StoreId"
UNION ALL SELECT 'Attendance punches', count(*) FROM "AttendancePunches" ap JOIN _garmetix_validation_context x ON x.company_id = ap."CompanyId" AND x.store_id = ap."StoreId"
UNION ALL SELECT 'Daily attendance rows', count(*) FROM "Attendance" a JOIN _garmetix_validation_context x ON x.company_id = a."CompanyId" AND x.store_id = a."StoreId";

\echo ''
\echo '12) Attendance IST/local time validation'
SELECT
  CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END AS status,
  count(*) AS punches_where_local_time_looks_same_as_utc
FROM "AttendancePunches" ap JOIN _garmetix_validation_context x ON x.company_id = ap."CompanyId" AND x.store_id = ap."StoreId"
WHERE abs(extract(epoch from (ap."LocalPunchTime" - ap."PunchTimeUtc"))) < 60;

SELECT ap."EmployeeId", e."FirstName", e."LastName", ap."PunchType", ap."PunchTimeUtc", ap."LocalPunchTime", ap."Source"
FROM "AttendancePunches" ap
JOIN _garmetix_validation_context x ON x.company_id = ap."CompanyId" AND x.store_id = ap."StoreId"
LEFT JOIN "Employees" e ON e."Id" = ap."EmployeeId"
WHERE abs(extract(epoch from (ap."LocalPunchTime" - ap."PunchTimeUtc"))) < 60
ORDER BY ap."PunchTimeUtc" DESC
LIMIT 50;

\echo ''
\echo '13) Punch-to-attendance sync validation'
WITH punch_days AS (
  SELECT ap."EmployeeId", ap."LocalPunchTime"::date AS punch_date, min(ap."LocalPunchTime") AS first_punch, max(ap."LocalPunchTime") AS last_punch
  FROM "AttendancePunches" ap JOIN _garmetix_validation_context x ON x.company_id = ap."CompanyId" AND x.store_id = ap."StoreId"
  GROUP BY ap."EmployeeId", ap."LocalPunchTime"::date
)
SELECT
  CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'FAIL' END AS status,
  count(*) AS punch_days_missing_attendance_row
FROM punch_days pd
JOIN _garmetix_validation_context x ON true
WHERE NOT EXISTS (
  SELECT 1 FROM "Attendance" a
  WHERE a."CompanyId" = x.company_id AND a."StoreId" = x.store_id
    AND a."EmployeeId" = pd."EmployeeId" AND a."OnDate"::date = pd.punch_date
);

SELECT e."FirstName", e."LastName", pd.punch_date, pd.first_punch, pd.last_punch
FROM (
  SELECT ap."EmployeeId", ap."LocalPunchTime"::date AS punch_date, min(ap."LocalPunchTime") AS first_punch, max(ap."LocalPunchTime") AS last_punch
  FROM "AttendancePunches" ap JOIN _garmetix_validation_context x ON x.company_id = ap."CompanyId" AND x.store_id = ap."StoreId"
  GROUP BY ap."EmployeeId", ap."LocalPunchTime"::date
) pd
JOIN _garmetix_validation_context x ON true
LEFT JOIN "Employees" e ON e."Id" = pd."EmployeeId"
WHERE NOT EXISTS (
  SELECT 1 FROM "Attendance" a
  WHERE a."CompanyId" = x.company_id AND a."StoreId" = x.store_id
    AND a."EmployeeId" = pd."EmployeeId" AND a."OnDate"::date = pd.punch_date
)
ORDER BY pd.punch_date DESC, e."FirstName"
LIMIT 50;

\echo ''
\echo '14) Active employees without direct employee shift rule - info only, category/gender rules may cover them'
SELECT e."EmployeeCode", e."FirstName", e."LastName", e."Department", e."Designation", e."Gender", e."Category"
FROM "Employees" e JOIN _garmetix_validation_context x ON x.company_id = e."CompanyId" AND x.store_id = e."StoreId"
WHERE COALESCE(e."Working", false) = true
  AND COALESCE(e."EmployeeStatus", 'Active') NOT IN ('Resigned','Terminated','Inactive')
  AND NOT EXISTS (
    SELECT 1 FROM "EmployeeAttendanceShiftRules" r
    WHERE r."CompanyId" = x.company_id AND r."StoreId" = x.store_id AND r."EmployeeId" = e."Id" AND r."Active" = true
  )
ORDER BY e."FirstName", e."LastName"
LIMIT 100;

\echo ''
\echo 'Validation complete. Review FAIL/WARN rows above before final production import.'
