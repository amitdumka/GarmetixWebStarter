# Garmetix Production TODO / Next Stages

Updated: 2026-06-25
Current package after this update: `v4.11.36` / `GARMETIX-11D21-20260625-4136`


## Stage 11D-19 completed in v4.11.36

- [x] Added `scripts/production/validate-production-data.sh`.
- [x] Added SQL validation for purchase import, stock, vendor, GST-ready totals, attendance punch sync, and shift-rule readiness.
- [x] Added report output under `/reports/production-validation/`.

## Stage 11D-17 completed in v4.11.33

- [x] Mark remaining production work into this TODO file.
- [x] Fix attendance punch local time so manual/kiosk punches use India time (`Asia/Kolkata`) instead of Docker/server UTC.
- [x] Kiosk now sends both UTC punch time and India local punch time.
- [x] Backend now converts UTC/local request values to Indian local time before saving `AttendancePunches.LocalPunchTime` and daily attendance check-in/check-out time.
- [x] Today attendance default date now uses India current date.

## Highest priority validation

- [ ] Build and deploy latest ZIP successfully.
- [ ] Verify API `/api/app-info` reports frontend/backend `4.11.33`.
- [ ] Verify manual punch time in Attendance page shows current India time.
- [ ] Verify kiosk punch time in Attendance page shows current India time.
- [ ] Verify auto punch sequence: CheckIn → BreakOut → BreakIn → CheckOut for break shifts.
- [ ] Verify no-break shifts: CheckIn → CheckOut.
- [ ] Verify duplicate punch returns friendly duplicate result, not Vue 400 error.

## Purchase import finalization

- [ ] Restore database to before old purchase import if production totals need correction.
- [ ] Run Aadwika Purchase Import v7 fresh.
- [ ] Validate discount-percent calculation.
- [ ] Validate support/additional cost as freight and stock cost.
- [ ] Validate inward format: `SM/YYYYMM/INW/0001`.
- [ ] Validate vendor outstanding after import.
- [ ] Validate stock quantity and value.
- [ ] Validate GST/input tax register.
- [ ] Validate zero-stock fallback rule.

## Employee + attendance import replanning

- [ ] Recreate employee import plan after final shift rules are confirmed.
- [ ] Import active/inactive employees with joining/leaving dates.
- [ ] Import shift assignments for Male, Female, Accounts, Housekeeping.
- [ ] Import attendance from April 2024 onward.
- [ ] Decide whether imported history should create `AttendancePunches`, `Attendance`, or both.
- [ ] Confirm Geetanjali no attendance after leaving date.
- [ ] Confirm left employees only get attendance up to leave date.

## Attendance shift/payroll final testing

- [ ] Male/default shift: 09:00-21:00 with lunch break 13:00-14:30.
- [ ] Female shift: 10:00-20:00.
- [ ] Accounts shift.
- [ ] Housekeeping double shift.
- [ ] One completed housekeeping shift = HalfDay.
- [ ] Two completed housekeeping shifts = FullDay.
- [ ] Admin/Owner can edit CheckIn/BreakOut/BreakIn/CheckOut.
- [ ] Add attendance correction audit history for edits.
- [ ] Confirm monthly attendance/payroll calculation after edits.

## Master data pages validation

- [ ] Purchase → Vendors: add/edit/delete/inactivate.
- [ ] Inventory → Brands: add/edit/delete/inactivate.
- [ ] Inventory → Categories: add/edit/delete/inactivate.
- [ ] Inventory → Sub-categories: add/edit/delete/inactivate.
- [ ] Used master rows should soft-delete/inactivate, not hard-delete.
- [ ] Normal users should not see restricted master setup pages.

## Purchase/Sale PDF testing

- [ ] Test purchase invoice PDF with 1-5 rows.
- [ ] Test purchase invoice PDF with 20-40 rows.
- [ ] Test purchase invoice PDF with 100+ rows.
- [ ] Test sale invoice PDF A4.
- [ ] Test sale invoice PDF A5.
- [ ] Confirm page-wise summary.
- [ ] Confirm last-page grand total.
- [ ] Confirm no overlap in print preview.

## Inventory filters extension

- [ ] Add brand filter.
- [ ] Add vendor filter.
- [ ] Add size filter.
- [ ] Add color filter.
- [ ] Add stock ageing buckets: 0-30, 31-60, 61-90, 90+ days.
- [ ] Add dead-stock filter.
- [ ] Add low-stock filter.
- [ ] Add high-value-stock filter.
- [ ] Add missing HSN/image/data-quality filters.

## Accounting/GST validation after imports

- [ ] Purchase register.
- [ ] GST input tax report.
- [ ] Vendor payable.
- [ ] Stock valuation.
- [ ] Profit calculation.
- [ ] Accounting postings.

## Final production acceptance

- [ ] Factory reset works.
- [ ] Seed Aadwika Fashion + Smart Menswear.
- [ ] Import employees.
- [ ] Import attendance.
- [ ] Import purchase inward.
- [ ] Create sale invoice.
- [ ] Create purchase invoice.
- [ ] Print A4/A5 PDFs.
- [ ] Check stock report.
- [ ] Check vendor outstanding.
- [ ] Check monthly attendance/payroll.
- [ ] Test backup and restore.


## Stage 11D-20 completed in v4.11.36

- Added advanced Inventory filters.
- Remaining: run filter tests after real purchase import, verify low stock/dead stock/high value counts, and adjust thresholds if required.


## Stage 11D-24 completed in v4.11.39
- Added shift-aware Aadwika Smart Menswear employee + attendance import toolkit.
- Added employee-specific shift rule import.
- Added generated AttendancePunches + daily Attendance rows in IST/UTC format.
- Added pre-import backup, restore script and validation script.

## Still pending after 11D-24
- Run import on production backup and validate monthly attendance/payroll.
- Add attendance correction audit history for admin timing edits.
- Finalize purchase import v7 on clean/restore database and validate accounting/GST.
