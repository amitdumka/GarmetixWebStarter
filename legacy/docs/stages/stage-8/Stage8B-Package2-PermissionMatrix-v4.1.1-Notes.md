# Stage 8B Package 2 - Role Permission Matrix

Version: 4.1.1
Build: `GARMETIX-8B-20260614-4101`
Date: 2026-06-14

## Completed

- Replaced scattered ASP.NET authorization assertions and role arrays with one server-owned permission matrix.
- Made Owner access universal across Admin, edit, delete, and operational module policies.
- Preserved Store Manager view and entry access for store modules while denying Admin, global edit, delete, and Payroll rights.
- Preserved edit rights for Owner, Admin, Power User, and Accountant and delete rights for Owner/Admin only.
- Added dedicated HR and Payroll login roles without changing existing enum values.
- Added HR/Payroll frontend route recognition and direct post-login landing in the assigned module.
- Added an API endpoint and responsive Access workspace table showing effective module and action permissions.
- Removed the internal Admin field from Access import/export and added active-status import/export.
- Added an xUnit test project with ten passing role authorization cases.

## Preserved Behavior

- Existing user and workspace scoping remains unchanged.
- Existing role numeric values remain stable; HR and Payroll were appended.
- Existing module endpoints retain their named authorization policies.

## Next

Verify the permission-aware shell, mobile navigation, command palette, and notification/action experience.
