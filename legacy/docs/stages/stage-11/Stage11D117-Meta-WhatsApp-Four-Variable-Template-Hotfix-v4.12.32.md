# Stage 11D-117 — Meta WhatsApp Four Variable Template Hotfix

Version: v4.12.32

## Purpose

Meta template creation for Aadwika invoice messages disabled Save when the template used 5 variables. This stage aligns Garmetix invoice WhatsApp sending with the approved 4-variable fixed-store template.

## Approved invoice template

Use this Utility template in Meta:

```text
Hello {{1}},
Thank you for shopping at Aadwika Fashion.

Your invoice {{2}} of ₹ {{3}} is ready.

View / download your bill:
{{4}}

Team Aadwika Fashion, Dumka.
```

Suggested template name:

```text
garmetix_invoice_link
```

Language code:

```text
en
```

## Garmetix variable mapping

- `{{1}}` customer name
- `{{2}}` invoice number
- `{{3}}` amount
- `{{4}}` public digital bill URL

Store name is fixed in the approved Meta template text, so the API sender no longer submits store name as a fifth body parameter for invoice messages.

## Files changed

- `backend/Garmetix.Api/Marketing/DigitalBillWhatsAppService.cs`
- `frontend/garmetix-web/pages/marketing/whatsapp-settings.vue`
- version metadata
