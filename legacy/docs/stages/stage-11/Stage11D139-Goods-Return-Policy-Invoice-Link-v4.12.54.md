# Stage 11D-139 — Goods Return Policy Invoice Link — v4.12.54

## Scope
- Added public `/goods-return-policy` page for Aadwika Fashion / Garmetix customers.
- Linked Goods Return / Exchange Policy from public digital invoice page.
- Added policy URL and brief no-refund / exchange-only note to generated sale invoice PDFs.
- Public digital invoice API now exposes `goodsReturnPolicyUrl`.

## Policy summary
- Goods once sold are not refundable.
- Eligible goods can be exchanged within 7 days with invoice proof.
- Goods must be unused, in original condition, with intact price tag and original packing.
- Approved product return to credit issues a store credit note, not money refund.
- Credit note is valid until 31 March of the same financial year, or 6 months if issued in Jan/Feb/Mar.

## Deployment
Normal web/API rebuild is required because frontend and API/PDF files changed.
