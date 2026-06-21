# Payroll PDF Download Guard - v4.10.27

## Purpose

Salary payment slips could be printed after save, but the Salary Payments register did not provide a direct PDF download action. The generated salary payment voucher number uses `StoreCode/YYYYMM/SPAY/series`, so the PDF filename also needed sanitizing before download.

## Implemented

- Salary Payment table rows now expose a direct `Download` action.
- The frontend downloads through `useServerDocumentPrint().downloadPdf(...)`, matching the hosted-safe print/download path used by vouchers and payslips.
- The frontend and backend sanitize salary payment PDF filenames so slash-based voucher numbers become safe file names.
- A payroll acceptance guard now checks salary payment preview, advance recovery, previous due, rounded payment amount, SPAY numbering, accounting posting, payslip sharing and payroll PDF QR output.

## Acceptance

Run:

```bash
python scripts/validation/payroll-acceptance-check.py
python scripts/validation/current-release-checks.py
```

