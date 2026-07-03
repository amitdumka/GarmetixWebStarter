\set ON_ERROR_STOP on
\echo 'Recalculating GST split from company/vendor/customer GSTIN'

CREATE OR REPLACE FUNCTION pg_temp.garmetix_gst_state_code(gstin text)
RETURNS text LANGUAGE sql IMMUTABLE AS $$
  SELECT CASE
    WHEN length(regexp_replace(coalesce(gstin,''), '[^A-Za-z0-9]', '', 'g')) = 15
      AND substring(upper(regexp_replace(coalesce(gstin,''), '[^A-Za-z0-9]', '', 'g')) from 1 for 2) ~ '^[0-9]{2}$'
    THEN substring(upper(regexp_replace(coalesce(gstin,''), '[^A-Za-z0-9]', '', 'g')) from 1 for 2)
    ELSE NULL
  END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.garmetix_is_inter_state(company_gstin text, party_gstin text)
RETURNS boolean LANGUAGE sql IMMUTABLE AS $$
  SELECT pg_temp.garmetix_gst_state_code(company_gstin) IS NOT NULL
     AND pg_temp.garmetix_gst_state_code(party_gstin) IS NOT NULL
     AND pg_temp.garmetix_gst_state_code(company_gstin) <> pg_temp.garmetix_gst_state_code(party_gstin);
$$;

\echo 'Before purchase split summary'
SELECT
  count(*) AS purchase_invoice_count,
  sum(coalesce("CGSTAmount",0)) AS cgst,
  sum(coalesce("SGSTAmount",0)) AS sgst,
  sum(coalesce("IGSTAmount",0)) AS igst,
  sum(coalesce("TaxAmount",0)) AS total_tax
FROM "PurchaseInvoices";

WITH purchase_item_split AS (
  SELECT
    ii."Id" AS item_id,
    pg_temp.garmetix_is_inter_state(c."GSTIN", v."GSTIN") AS inter_state,
    coalesce(ii."TaxAmount",0) AS tax_amount
  FROM "InvoiceItems" ii
  JOIN "PurchaseInvoices" pi ON pi."Id" = ii."InvoiceId"
  JOIN "Companies" c ON c."Id" = pi."CompanyId"
  LEFT JOIN "Vendors" v ON v."Id" = pi."VendorId"
  WHERE ii."Discriminator" = 'PurchaseInvoiceItem'
)
UPDATE "InvoiceItems" ii
SET "CGSTAmount" = CASE WHEN s.inter_state THEN 0 ELSE round(s.tax_amount / 2, 2) END,
    "SGSTAmount" = CASE WHEN s.inter_state THEN 0 ELSE s.tax_amount - round(s.tax_amount / 2, 2) END,
    "IGSTAmount" = CASE WHEN s.inter_state THEN s.tax_amount ELSE 0 END,
    "TaxType" = CASE WHEN s.inter_state THEN 3 ELSE 0 END,
    "UpdatedAt" = now()
FROM purchase_item_split s
WHERE ii."Id" = s.item_id;

WITH purchase_invoice_split AS (
  SELECT
    pi."Id" AS invoice_id,
    pg_temp.garmetix_is_inter_state(c."GSTIN", v."GSTIN") AS inter_state,
    coalesce(sum(ii."CGSTAmount"),0) AS cgst,
    coalesce(sum(ii."SGSTAmount"),0) AS sgst,
    coalesce(sum(ii."IGSTAmount"),0) AS igst,
    coalesce(sum(ii."TaxAmount"),0) AS tax_amount,
    coalesce(sum(ii."BasePrice"),0) AS taxable,
    coalesce(sum(ii."Amount"),0) AS net_amount
  FROM "PurchaseInvoices" pi
  JOIN "Companies" c ON c."Id" = pi."CompanyId"
  LEFT JOIN "Vendors" v ON v."Id" = pi."VendorId"
  LEFT JOIN "InvoiceItems" ii ON ii."InvoiceId" = pi."Id" AND ii."Discriminator" = 'PurchaseInvoiceItem'
  GROUP BY pi."Id", c."GSTIN", v."GSTIN"
)
UPDATE "PurchaseInvoices" pi
SET "CGSTAmount" = s.cgst,
    "SGSTAmount" = s.sgst,
    "IGSTAmount" = s.igst,
    "TaxAmount" = s.tax_amount,
    "BasePrice" = s.taxable,
    "NetAmount" = s.net_amount,
    "InterState" = s.inter_state,
    "UpdatedAt" = now()
FROM purchase_invoice_split s
WHERE pi."Id" = s.invoice_id;

\echo 'Before/after sale split recalculation will use CustomerGSTIN or linked Customer.GSTIN'
WITH sale_item_split AS (
  SELECT
    ii."Id" AS item_id,
    pg_temp.garmetix_is_inter_state(c."GSTIN", coalesce(inv."CustomerGSTIN", cust."GSTIN")) AS inter_state,
    coalesce(ii."TaxAmount",0) AS tax_amount
  FROM "InvoiceItems" ii
  JOIN "SalesInvoices" inv ON inv."Id" = ii."InvoiceId"
  JOIN "Companies" c ON c."Id" = inv."CompanyId"
  LEFT JOIN "Customers" cust ON cust."Id" = inv."CustomerId"
  WHERE coalesce(ii."Discriminator", '') <> 'PurchaseInvoiceItem'
)
UPDATE "InvoiceItems" ii
SET "CGSTAmount" = CASE WHEN s.inter_state THEN 0 ELSE round(s.tax_amount / 2, 2) END,
    "SGSTAmount" = CASE WHEN s.inter_state THEN 0 ELSE s.tax_amount - round(s.tax_amount / 2, 2) END,
    "IGSTAmount" = CASE WHEN s.inter_state THEN s.tax_amount ELSE 0 END,
    "TaxType" = CASE WHEN s.inter_state THEN 3 ELSE 0 END,
    "UpdatedAt" = now()
FROM sale_item_split s
WHERE ii."Id" = s.item_id;

WITH sale_invoice_split AS (
  SELECT
    inv."Id" AS invoice_id,
    pg_temp.garmetix_is_inter_state(c."GSTIN", coalesce(inv."CustomerGSTIN", cust."GSTIN")) AS inter_state,
    coalesce(sum(ii."CGSTAmount"),0) AS cgst,
    coalesce(sum(ii."SGSTAmount"),0) AS sgst,
    coalesce(sum(ii."IGSTAmount"),0) AS igst,
    coalesce(sum(ii."TaxAmount"),0) AS tax_amount,
    coalesce(sum(ii."BasePrice"),0) AS taxable,
    coalesce(sum(ii."Amount"),0) AS net_amount
  FROM "SalesInvoices" inv
  JOIN "Companies" c ON c."Id" = inv."CompanyId"
  LEFT JOIN "Customers" cust ON cust."Id" = inv."CustomerId"
  LEFT JOIN "InvoiceItems" ii ON ii."InvoiceId" = inv."Id" AND coalesce(ii."Discriminator", '') <> 'PurchaseInvoiceItem'
  GROUP BY inv."Id", c."GSTIN", inv."CustomerGSTIN", cust."GSTIN"
)
UPDATE "SalesInvoices" inv
SET "CGSTAmount" = s.cgst,
    "SGSTAmount" = s.sgst,
    "IGSTAmount" = s.igst,
    "TaxAmount" = s.tax_amount,
    "BasePrice" = s.taxable,
    "NetAmount" = s.net_amount,
    "InterState" = s.inter_state,
    "UpdatedAt" = now()
FROM sale_invoice_split s
WHERE inv."Id" = s.invoice_id;

\echo 'After purchase split summary'
SELECT
  count(*) AS purchase_invoice_count,
  sum(coalesce("CGSTAmount",0)) AS cgst,
  sum(coalesce("SGSTAmount",0)) AS sgst,
  sum(coalesce("IGSTAmount",0)) AS igst,
  sum(coalesce("TaxAmount",0)) AS total_tax
FROM "PurchaseInvoices";

\echo 'After sale split summary'
SELECT
  count(*) AS sale_invoice_count,
  sum(coalesce("CGSTAmount",0)) AS cgst,
  sum(coalesce("SGSTAmount",0)) AS sgst,
  sum(coalesce("IGSTAmount",0)) AS igst,
  sum(coalesce("TaxAmount",0)) AS total_tax
FROM "SalesInvoices";
