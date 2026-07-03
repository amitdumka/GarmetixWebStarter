\echo 'Stage 11D-30 GST/import validation started'

CREATE TEMP TABLE _stage11d30_checks (
  severity text NOT NULL,
  check_name text NOT NULL,
  issue_count integer NOT NULL,
  details text
);

-- Context check
INSERT INTO _stage11d30_checks
SELECT 'INFO', 'Company and store context', count(*)::int,
  string_agg(c."Name" || ' / ' || s."Name" || ' / ' || COALESCE(s."StoreCode",''), ', ')
FROM "Companies" c
JOIN "Stores" s ON s."CompanyId" = c."Id"
WHERE lower(c."Name") = lower('Aadwika Fashion')
  AND lower(s."Name") = lower('Smart Menswear');

-- Sale invoice number format check: StoreCode/YYYYMM/INV/series
INSERT INTO _stage11d30_checks
SELECT 'FAIL', 'Sale invoice number format StoreCode/YYYYMM/INV/series', count(*)::int,
  'Examples: ' || COALESCE(string_agg(x."InvoiceNumber", ', '), '-')
FROM (
  SELECT i."InvoiceNumber"
  FROM "SalesInvoices" i
  JOIN "Stores" s ON s."Id" = i."StoreId"
  WHERE COALESCE(i."Deleted", false) = false
    AND COALESCE(i."InvoiceNumber", '') !~ ('^' || COALESCE(NULLIF(s."StoreCode", ''), 'SM') || '/' || to_char(i."OnDate", 'YYYYMM') || '/INV/[0-9]{4,}$')
  ORDER BY i."OnDate" DESC
  LIMIT 20
) x;

-- Duplicate sale invoice numbers within company/store
INSERT INTO _stage11d30_checks
SELECT 'FAIL', 'Duplicate sale invoice numbers', count(*)::int,
  'Duplicate invoice groups: ' || count(*)::text
FROM (
  SELECT "CompanyId", "StoreId", "InvoiceNumber"
  FROM "SalesInvoices"
  WHERE COALESCE("Deleted", false) = false
  GROUP BY "CompanyId", "StoreId", "InvoiceNumber"
  HAVING count(*) > 1
) d;

-- Purchase inward number format check
INSERT INTO _stage11d30_checks
SELECT 'FAIL', 'Purchase inward number format StoreCode/YYYYMM/INW/series', count(*)::int,
  'Examples: ' || COALESCE(string_agg(x."InwardNumber", ', '), '-')
FROM (
  SELECT p."InwardNumber"
  FROM "PurchaseInvoices" p
  JOIN "Stores" s ON s."Id" = p."StoreId"
  WHERE COALESCE(p."Deleted", false) = false
    AND COALESCE(p."InwardNumber", '') !~ ('^' || COALESCE(NULLIF(s."StoreCode", ''), 'SM') || '/' || to_char(p."InwardDate", 'YYYYMM') || '/INW/[0-9]{4,}$')
  ORDER BY p."InwardDate" DESC
  LIMIT 20
) x;

-- GSTIN based interstate purchase split sanity
INSERT INTO _stage11d30_checks
SELECT 'WARN', 'Purchase invoices with vendor GSTIN missing/invalid', count(*)::int,
  'Missing or short vendor GSTIN prevents exact IGST/CGST decision.'
FROM "PurchaseInvoices" p
WHERE COALESCE(p."Deleted", false) = false
  AND (p."VendorGSTIN" IS NULL OR length(trim(p."VendorGSTIN")) < 2);

INSERT INTO _stage11d30_checks
SELECT 'FAIL', 'Purchase GST split mismatch by GSTIN state code', count(*)::int,
  'Company/vendor GSTIN state and invoice InterState/CGST/SGST/IGST mismatch.'
FROM "PurchaseInvoices" p
JOIN "Companies" c ON c."Id" = p."CompanyId"
WHERE COALESCE(p."Deleted", false) = false
  AND length(COALESCE(c."GSTIN",'')) >= 2
  AND length(COALESCE(p."VendorGSTIN",'')) >= 2
  AND (
    (substring(c."GSTIN" from 1 for 2) = substring(p."VendorGSTIN" from 1 for 2)
      AND (COALESCE(p."IGSTAmount",0) <> 0 OR COALESCE(p."CGSTAmount",0) = 0 OR COALESCE(p."SGSTAmount",0) = 0 OR p."InterState" = true))
    OR
    (substring(c."GSTIN" from 1 for 2) <> substring(p."VendorGSTIN" from 1 for 2)
      AND (COALESCE(p."IGSTAmount",0) = 0 OR COALESCE(p."CGSTAmount",0) <> 0 OR COALESCE(p."SGSTAmount",0) <> 0 OR p."InterState" = false))
  );

INSERT INTO _stage11d30_checks
SELECT 'FAIL', 'Purchase invoice totals vs invoice item totals', count(*)::int,
  'Invoices where items + freight + round off differs from BillAmount by more than 1 rupee.'
FROM (
  SELECT p."Id"
  FROM "PurchaseInvoices" p
  LEFT JOIN "InvoiceItems" ii ON ii."InvoiceId" = p."Id" AND COALESCE(ii."Deleted", false) = false AND COALESCE(ii."Discriminator", 'PurchaseInvoiceItem') = 'PurchaseInvoiceItem'
  WHERE COALESCE(p."Deleted", false) = false
  GROUP BY p."Id", p."BillAmount", p."FrightAmount", p."RoundOff"
  HAVING abs(COALESCE(sum(ii."Amount"), 0) + COALESCE(p."FrightAmount",0) + COALESCE(p."RoundOff",0) - COALESCE(p."BillAmount",0)) > 1
) bad;

INSERT INTO _stage11d30_checks
SELECT 'FAIL', 'Negative or zero imported stock where purchase qty exists', count(*)::int,
  'Stock rows with PurchaseQty <= 0 but product exists.'
FROM "Stocks" s
WHERE COALESCE(s."Deleted", false) = false
  AND COALESCE(s."PurchaseQty", 0) <= 0;

\echo 'Stage 11D-30 validation result'
SELECT * FROM _stage11d30_checks ORDER BY CASE severity WHEN 'FAIL' THEN 1 WHEN 'WARN' THEN 2 ELSE 3 END, check_name;
