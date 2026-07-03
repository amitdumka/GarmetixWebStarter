# Goods Return / Exchange Operational Acceptance — v4.12.57

This module provides operational QA for Aadwika/Garmetix goods return policy.

The page is available at:

- `/goods-return-acceptance`

The backend endpoints are:

- `GET /api/goods-return/acceptance`
- `GET /api/goods-return/acceptance/evidence.csv`

## Purpose

The system already supports sales returns, exchange invoices and customer credit notes. This stage adds a closure screen so store operators and owners can verify that posted return records follow the business policy:

- goods once sold cannot be refunded,
- exchange/credit only within 7 days,
- original invoice proof is required,
- credit note remains valid only as per policy,
- replacement exchange should consume return credit with evidence.

## Complete / Not Complete

The report is **Complete** only when there are no Critical or Warning issues.

Critical issues include:

- missing original sale invoice link,
- return outside 7-day policy,
- refund/payment evidence against a return,
- missing linked customer credit note,
- over-adjusted credit note,
- expired open credit.

Warnings include:

- credit-note total mismatch,
- open credit note not printed/shared,
- credit note expiring within 30 days,
- returned item barcode missing,
- open store credit with no replacement exchange invoice,
- replacement invoice without linked return-credit payment row.

## Operator limitations

Physical inspection evidence is still manual in this stage. The page does not capture returned product photos, tag images, packing images, stain/tear proof or owner exception approval attachments.
