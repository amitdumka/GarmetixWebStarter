# Nuxt UI Implementation Stages

This is the staged frontend plan for moving Garmetix to Nuxt 4 + Nuxt UI v4.

## Stage 1 - Foundation and Dashboard Shell

Status: Done

- Add `@nuxt/ui` and `tailwindcss`.
- Register the Nuxt UI module in `nuxt.config.ts`.
- Wrap the app in `UApp`.
- Replace the custom dashboard shell with `UDashboardGroup`, `UDashboardSidebar`, `UDashboardPanel`, `UDashboardNavbar`, `UNavigationMenu`, `USelect`, `UButton`, `UTooltip`, and `UColorModeButton`.
- Replace the login and first-admin card with `UCard`, `UButton`, `UFormField`, `UInput`, and `UAlert`.
- Keep existing module pages working while the shared shell changes.

## Stage 2 - Shared CRUD Building Blocks

Status: Done

- Add reusable page header, table toolbar, empty state, confirm delete modal, and form slideover components.
- Standardize list/search/filter/loading/error states.
- Add toast notifications for save, update, delete, cancel, and generate actions.

## Stage 3 - Core Store Modules

Status: Pending

- Convert Setup, Inventory, Billing, Purchase, Vouchers, and Petty Cash pages to Nuxt UI components.
- Use `UCard`, `UTable`, `UBadge`, `UForm`, `UFormField`, `UInput`, `USelect`, `UInputNumber`, `UTextarea`, `UModal`, and `USlideover`.
- Add consistent action menus for edit, delete, print, cancel, and duplicate.

## Stage 4 - HR and Payroll

Status: Pending

- Convert Employees, Daily Attendance, Monthly Attendance, Salary Structures, and Salary Payments to Nuxt UI tabs, tables, forms, and dialogs.
- Add monthly attendance generation feedback with progress and toast result.
- Add payroll-ready attendance summary display.

## Stage 5 - Access, Import/Export, and Audit

Status: Pending

- Convert user access management to Nuxt UI tables and forms.
- Add role badges, scoped company/store selectors, and password reset dialogs.
- Build import/export screens with file upload, validation result table, and export actions.

## Stage 6 - Reports and Deployment Polish

Status: Pending

- Add report dashboards with date filters, store filters, print/export actions, and summary cards.
- Add responsive QA pass for desktop, tablet, and mobile dashboard layouts.
- Finalize Docker, Linux, and Mac mini deployment notes.
