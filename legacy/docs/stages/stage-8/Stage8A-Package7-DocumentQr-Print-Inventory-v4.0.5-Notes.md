# Stage 8A Package 7 - v4.0.5

Date: 2026-06-13

## Scope

- Standardize Inventory and Stock Operations UI.
- Add stable QR identity and permission-aware document scanning.
- Add guarded factory reset.
- Replace dashboard-DOM printing with server PDF printing.
- Add Petty Cash, payslip, and salary-payment PDFs.

## Delivered

- Added `GMX:<TYPE>:<GUID>` document tokens and QR rendering through QRCoder.
- Added `/document-scan` with manual/USB input and optional camera scanning.
- Extended scan lookup to sales, purchases, vouchers, cash vouchers, commercial notes, Petty Cash, payslips, and salary payments.
- Embedded QR codes in sale, purchase, voucher, cash-voucher, and debit/credit-note PDFs.
- Added A5 landscape Petty Cash PDF, A4 payslip PDF, and A5 salary-payment slip PDF.
- Added a shared authenticated PDF print/download composable.
- New sales, purchases, vouchers, cash vouchers, notes, Petty Cash sheets, and salary payments invoke PDF printing only after create.
- Added Admin/Owner factory reset with typed confirmation, automatic safety backup, migration-history preservation, and current-admin preservation.
- Standardized Inventory and Stock Operations registers; Product Master now uses a wide modal workspace.
- Replaced the optional vendor's empty select value with an internal sentinel so the Product workspace opens without a Nuxt UI runtime error.

## Safety

- Factory reset was implemented and compiled but was not executed against the developer database.
- All scan lookups apply existing workspace scope rules.
