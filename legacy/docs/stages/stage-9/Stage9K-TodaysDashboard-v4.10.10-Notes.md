# Stage 9K - Today's Dashboard v4.10.10

Version: `4.10.10`  
Stage: `Stage 9K Today's Dashboard`  
Build: `GARMETIX-9K-20260620-4110`

## Scope

Adds one new Dashboard section page named **Today's** before moving to further modules.

## Added

- New route: `/dashboard/todays`
- New API: `GET /api/dashboard/todays`
- Sales, purchase, receipts, payments, expenses and cash voucher metrics for selected business date.
- Active employee present/absent list based on Stage 9 attendance punches.
- Last-14-day sales, purchase and gross-margin line graph data.
- Cash flow breakdown for sales collections, purchase payments, vouchers and cash vouchers.
- Recent activity list for sales, purchases, vouchers and cash vouchers.
- Dashboard navigation and route-access coverage.
- Test automation manifest item: `TODAYS_DASHBOARD_ACCEPTANCE`.
- Host drill: `scripts/linux/todays-dashboard-drill.sh`.

## Notes

- Employee absent list includes only active/working employees.
- No payroll posting, accounting posting or new database migration was added.
- The page respects existing workspace/company/store scoping.
