# Stage 8C Package 3 - Vendor Settlement

Version: 4.2.2
Build: `GARMETIX-8C-20260615-4220`
Date: 2026-06-15

## Delivered

- Added formal vendor settlement and purchase-invoice allocation records.
- Added server-owned `StoreCode/YYYYMM/VSET/series` settlement numbering.
- Added debit-note adjustment across one or more outstanding purchases.
- Added cash and bank vendor-refund receipt workflows.
- Added mixed settlement support where part is adjusted and part is refunded.
- Added `StoreCode/YYYYMM/VREF/series` receipt voucher generation for refunds.
- Added balanced vendor refund journals, bank transactions, statement rows, and cheque logs through the existing accounting service.
- Linked settlement records to the purchase return, debit note, vendor, purchase invoices, voucher, journal, bank transaction, and allocation history.
- Added a dedicated Vendor Settlements page with open-credit and settlement-history registers.
- Added automatic first print for generated refund vouchers and direct receipt reprint.
- Added persistent settlement status and available-credit values to Purchase Return.

## Accounting Rule

The purchase-return debit note already debits the vendor ledger. Allocating that note to outstanding purchase invoices is therefore a tracking allocation and does not post the ledger a second time. An actual refund debits cash or bank and credits the vendor ledger.

## Next

Stage 8C Package 4 will harden exact item-level input-tax-credit reversal and complete end-to-end reconciliation links across purchase, return, stock, GST, ledger, payment, and audit history.
