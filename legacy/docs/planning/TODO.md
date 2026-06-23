## Completed in v4.9.11

- Added Cash Details register page for store-wise note/coin cash history.
- Added Cash Details API for listing, history, add, edit and delete.
- Manual cash-flow cash detail rows can be added/edited/deleted.
- Day Opening/Day Closing linked cash details can be edited safely and sync linked DayBegin/DayEnd/PettyCashSheet values.
- Delete is blocked for Day Opening/Closing linked cash details to avoid broken day records.
- Added source filters: ManualCashFlow, DayOpening, DayClosing, StoreHoliday, CashVerification.
- Added Accounting menu entry for Cash Details.

## Completed in v4.9.10

- Fixed Store Day Petty Cash print opening `http://localhost:3000/api/.../pdf`.
- Store Day print now uses server PDF print iframe instead of opening raw PDF URL.
- PDF document URL builder now normalizes localhost/127.0.0.1 API base to the current production origin.
- Existing Petty Cash page PDF print/download also benefits from normalized API base.

## Completed in v4.9.9

- Fixed Store Day frontend `success is not a function` runtime error after successful open/close/holiday.
- Added `success()` alias to UI feedback composable for backward compatibility.
- Petty cash auto calculation now includes Cash Voucher receipts/payments/expenses along with Voucher records.
- Petty cash print now includes a second A5 page for book transaction details.
- Added Day Close reopen and delete-close actions.
- Added one-attendance-per-employee-per-day guard.
- Added one-petty-cash-sheet-per-store-per-day guard in generic CRUD path.
- Added validation coverage for store day reopen/delete and petty cash transaction detail print.

## Completed in v4.9.8

- Added per-store Day Opening and Day Closing.
- Added cash denomination/details for opening and closing.
- Day Closing generates book summary from sales, receipts, vouchers, bank cash and previous closing.
- Day Closing creates/updates Petty Cash Sheet and Cash Details.
- Added Store Holiday/Closed Day flow that carries previous closing to opening/closing and blocks entries.
- Added mandatory day-open/day-close guard for Store Manager and Billing/Sales users only.
- Admin and Accountant users are not blocked by the day guard.
- Added Store Day Open / Close page and top-menu link.

## Completed in v4.9.7

- Fixed workspace/store selection jumping back to first store after page refresh.
- Active company/store group/store now persists per user/browser session.
- Added "Set as default" workspace action.
- Top bar workspace pill is clickable and shows selected store name.
- Workspace selector modal provides one place to change Company, Store Group and Store.
- Small-screen users can change workspace through the same top bar workspace pill/modal.

## Completed in v4.9.6

- Fixed compile error in PortableSeederEndpoints by adding missing helper methods.
- Fixed compile error from invalid CreatedBy on Company/StoreGroup/Store setup models.
- Fixed nullable column list warning in PortableSeederEndpoints import metadata.
- Added validation to catch missing portable seeder helper methods and invalid setup CreatedBy assignments.

## Completed in v4.9.5

- Fixed AF/SS seeder comparison syntax error in `Seeder2CsOnly` string list.
- Added admin-only seeder/merge verification endpoint.
- Added AF/SS page verification card for Aadwika Fashion, Smart Menswear, Shalini separation and default accounting masters.
- Added verification after AF/SS seed, portable import or Aadwika + Smart merge.

## Completed in v4.9.4

- Added admin-only Aadwika Fashion + Smart Menswear merge utility.
- Preview counts existing Smart/Samrat Menswear scoped rows before merge.
- Apply moves Smart/Samrat CompanyId rows to Aadwika Fashion.
- Apply moves Smart/Samrat StoreGroupId rows to Aadwika Fashion MBO.
- Smart Menswear store is attached to Aadwika Fashion MBO.
- Aadwika Fashion - Shalini remains separate and excluded.

## Completed in v4.9.3

- Default Indian accounting ledger groups/ledgers win when portable seeder data has name clashes.
- Portable JSON export removes protected default ledger groups/ledgers from seeder data.
- Portable JSON import skips protected default ledger/group rows and reapplies system defaults.
- Custom ledgers that used skipped default groups are remapped to the recreated system default group.
- AF/SS structure merged Aadwika Fashion Amit Kumar and Smart Menswear into one company/store group.
- Aadwika Fashion - Shalini remains a separate company/profile.

# Garmetix TODO

## Completed in v4.9.2

- Portable JSON database seeder export/import for admin crash recovery and system migration.
- AF/SS seeder can create company, store group and store automatically from selected profile.
- AF/SS seeder can update existing default data when existing company/profile is reused.
- Create seeder file from current data.
- Import portable seeder JSON into a fresh/crashed system.
- Auto default Indian accounting ledger groups/ledgers when a company is created.
- Protected default accounting ledgers and ledger groups from delete.
- Cascade soft-delete support for company, store group and store delete.
- Admin-only access for portable seeder operations.

## Still to verify after deploy

- Export portable JSON from live Mac mini.
- Import portable JSON into a test/fresh database.
- Run Data Consistency after import.
- Create company from normal Company Setup and confirm default accounting masters are created.
- Try deleting default ledger/group and confirm it is blocked.
- Try deleting test company/store group/store and confirm child rows are soft deleted.
