\echo 'Installing Garmetix invoice correction audit objects'

CREATE TABLE IF NOT EXISTS "InvoiceCorrectionAudits" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "TableName" text NOT NULL,
    "Operation" text NOT NULL,
    "InvoiceId" uuid NULL,
    "InvoiceNumber" text NULL,
    "SourceType" text NULL,
    "OldValues" jsonb NULL,
    "NewValues" jsonb NULL,
    "ChangedBy" text NULL DEFAULT current_user,
    "Remarks" text NULL
);

CREATE INDEX IF NOT EXISTS "IX_InvoiceCorrectionAudits_CreatedAt" ON "InvoiceCorrectionAudits" ("CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_InvoiceCorrectionAudits_InvoiceId" ON "InvoiceCorrectionAudits" ("InvoiceId");
CREATE INDEX IF NOT EXISTS "IX_InvoiceCorrectionAudits_TableName" ON "InvoiceCorrectionAudits" ("TableName", "Operation");

CREATE OR REPLACE FUNCTION garmetix_invoice_correction_audit_trigger()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_invoice_id uuid;
    v_invoice_number text;
    v_source_type text;
    v_old jsonb;
    v_new jsonb;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_old := to_jsonb(OLD);
        v_new := NULL;
    ELSIF TG_OP = 'UPDATE' THEN
        v_old := to_jsonb(OLD);
        v_new := to_jsonb(NEW);
    ELSE
        v_old := NULL;
        v_new := to_jsonb(NEW);
    END IF;

    IF TG_TABLE_NAME = 'InvoiceItems' THEN
        IF TG_OP = 'DELETE' THEN
            v_invoice_id := OLD."InvoiceId";
            v_source_type := COALESCE(OLD."Discriminator", 'InvoiceItem');
        ELSE
            v_invoice_id := NEW."InvoiceId";
            v_source_type := COALESCE(NEW."Discriminator", 'InvoiceItem');
        END IF;

        SELECT COALESCE(pi."InvoiceNumber", si."InvoiceNumber")
          INTO v_invoice_number
          FROM (SELECT v_invoice_id AS id) src
          LEFT JOIN "PurchaseInvoices" pi ON pi."Id" = src.id
          LEFT JOIN "SalesInvoices" si ON si."Id" = src.id;
    ELSE
        IF TG_OP = 'DELETE' THEN
            v_invoice_id := OLD."Id";
            v_invoice_number := OLD."InvoiceNumber";
        ELSE
            v_invoice_id := NEW."Id";
            v_invoice_number := NEW."InvoiceNumber";
        END IF;
        v_source_type := TG_TABLE_NAME;
    END IF;

    INSERT INTO "InvoiceCorrectionAudits" (
        "TableName",
        "Operation",
        "InvoiceId",
        "InvoiceNumber",
        "SourceType",
        "OldValues",
        "NewValues",
        "ChangedBy",
        "Remarks"
    ) VALUES (
        TG_TABLE_NAME,
        TG_OP,
        v_invoice_id,
        v_invoice_number,
        v_source_type,
        v_old,
        v_new,
        current_user,
        'Database-level invoice correction audit'
    );

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS "TRG_SalesInvoices_CorrectionAudit" ON "SalesInvoices";
CREATE TRIGGER "TRG_SalesInvoices_CorrectionAudit"
AFTER UPDATE OR DELETE ON "SalesInvoices"
FOR EACH ROW EXECUTE FUNCTION garmetix_invoice_correction_audit_trigger();

DROP TRIGGER IF EXISTS "TRG_PurchaseInvoices_CorrectionAudit" ON "PurchaseInvoices";
CREATE TRIGGER "TRG_PurchaseInvoices_CorrectionAudit"
AFTER UPDATE OR DELETE ON "PurchaseInvoices"
FOR EACH ROW EXECUTE FUNCTION garmetix_invoice_correction_audit_trigger();

DROP TRIGGER IF EXISTS "TRG_InvoiceItems_CorrectionAudit" ON "InvoiceItems";
CREATE TRIGGER "TRG_InvoiceItems_CorrectionAudit"
AFTER UPDATE OR DELETE ON "InvoiceItems"
FOR EACH ROW EXECUTE FUNCTION garmetix_invoice_correction_audit_trigger();

\echo 'Invoice correction audit installed.'
