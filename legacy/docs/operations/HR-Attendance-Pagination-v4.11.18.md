# Factory Reset Admin Identity Fix Performance Fix v4.11.19

## Purpose

After importing historical attendance from 2024 onward, the `/hr` page must not load all attendance rows into the browser. This patch makes the Attendance tab month-scoped and server-side paginated.

## Backend

- Added `GET /api/hr/attendance`.
- Query params: `year`, `month`, `page`, `pageSize`, optional `employeeId`, optional numeric `status`.
- Defaults to the current server month/year.
- Clamps `pageSize` to 10-200.
- Uses `AsNoTracking()`.
- Filters by workspace and date range before counting or paging.
- Returns `{ items, total, page, pageSize, year, month }`.

## Database

Added attendance lookup indexes:

- `CompanyId, StoreId, OnDate`
- `CompanyId, StoreId, EmployeeId, OnDate`

## Frontend

- `/hr` Attendance tab defaults to current month and year.
- Attendance rows are fetched only when the Attendance tab is active.
- Added month/year/employee/status/page-size filters.
- Added Previous/Next pagination.
- Removed the full `api.list('attendance')` load from HR page refresh.

## Acceptance

- Opening `/hr` is fast even after full historical attendance import.
- Attendance tab initially shows only the current month.
- Changing month/year reloads that month only.
- Pagination works without browser-side full-history filtering.
