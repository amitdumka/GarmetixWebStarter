# Nuxt UI Implementation Stages

This is the staged frontend plan for moving Garmetix to Nuxt 4 + Nuxt UI v4.

## Stage 1 - Foundation and Dashboard Shell

Status: Done

- Add `@nuxt/ui` and `tailwindcss`.
- Register the Nuxt UI module in `nuxt.config.ts`.
- Wrap the app in `UApp`.
- Replace the custom dashboard shell with `UDashboardGroup`, `UDashboardSidebar`, `UDashboardPanel`, `UDashboardNavbar`, `UNavigationMenu`, `USelect`, `UButton`, `UTooltip`, and `UColorModeButton`.
- Replace the login and first-admin card with `UCard`, `UButton`, `UFormField`, `UInput`, and `UAlert`.
- Set dark mode as the default color mode and add top-bar theme switching for Dark, Light, and System.
- Keep existing module pages working while the shared shell changes.

## Stage 2 - Shared CRUD Building Blocks

Status: Done

- Add reusable page header, table toolbar, empty state, confirm delete modal, and form slideover components.
- Standardize list/search/filter/loading/error states.
- Add toast notifications for save, update, delete, cancel, and generate actions.

## Stage 3 - Core Store Modules

Status: Done

- Convert the overview dashboard to the Nuxt Planner-inspired layout with grouped navigation, KPI cards, status panels, a current work table, and recent activity.
- Convert Setup to Nuxt UI with summary cards, tabbed master data, `UTable`, shared toolbar, slideover forms, toast feedback, and delete confirmation.
- Convert Inventory to Nuxt UI with stock KPI cards, product search, `UTable`, product slideover forms, toast feedback, and delete confirmation.
- Convert Vouchers to Nuxt UI with payment/receipt/expense KPI cards, searchable `UTable`, voucher slideover forms, toast feedback, and delete confirmation.
- Convert Petty Cash to Nuxt UI with daily cash KPI cards, searchable `UTable`, cash sheet slideover forms, calculated cash summary, toast feedback, and delete confirmation.
- Convert Purchase to Nuxt UI with purchase KPI cards, searchable purchase register, inward slideover workflow, cart totals, and toast feedback.
- Convert Billing to Nuxt UI with sales KPI cards, searchable invoice register, POS slideover workflow, receipt modal, cancel confirmation, print action, and toast feedback.
- Use `UCard`, `UTable`, `UBadge`, `UFormField`, `UInput`, `USelect`, `UTextarea`, `UModal`, and `USlideover` across core store modules.
- Add consistent actions for edit, delete, print, cancel, receipt, and save flows.

## Stage 4 - HR and Payroll

Status: Done

- Convert HR to Nuxt UI with employee, daily attendance, and monthly attendance tabs; KPI cards; `UTable`; slideover forms; monthly generation controls; toast feedback; and delete confirmation.
- Convert Payroll to Nuxt UI with salary structure and salary payment tabs; KPI cards; `UTable`; slideover forms; salary summaries; billable-days attendance context; toast feedback; and delete confirmation.
- Convert Employees, Daily Attendance, Monthly Attendance, Salary Structures, and Salary Payments to Nuxt UI tabs, tables, forms, and dialogs.
- Add monthly attendance generation feedback with progress and toast result.
- Add payroll-ready attendance summary display.
- Add payroll payslip generation, first-day auto generation, print/PDF view, Email and WhatsApp sharing, salary advance reduction, and due carry-forward.
- Add backend hosted automation for month-end monthly attendance generation and first-day previous-month payslip generation.

## Stage 5 - Access, Import/Export, and Audit

Status: In progress

- Convert user access management to Nuxt UI tables and forms.
- Add role badges, scoped company/store selectors, and password reset dialogs.
- Build import/export screens with file upload, validation result table, and export actions.
- Add admin-only CSV export and import template endpoints for setup, inventory, billing, purchase, vouchers, petty cash, HR, payroll, and access.
- Add `/import-export` Nuxt UI page with module export cards, template downloads, and CSV preview for the import validation step.
- Add validated CSV import commit flow for inventory, HR employees, vouchers, and petty cash with dry-run validation, row errors, created/updated counts, and blocked writes when errors exist.
- Complete `/access` Nuxt UI conversion with KPI cards, searchable user table, role badges, form slideover, password reset dialog, scoped selectors, toast feedback, and delete confirmation.
- Add `/audit` Nuxt UI page and admin-only `/api/audit/recent` activity feed from existing created/updated timestamps across major modules.

## Stage 6 - Reports and Deployment Polish

Status: In progress

- Add report dashboards with date filters, store filters, print/export actions, and summary cards.
- Add `/reports` Nuxt UI page with Sales, Purchase, Stock, Petty Cash, Attendance, and Payroll tabs, KPI cards, searchable tables, print, and CSV export.
- Verify Nuxt production build with local Node.js path and `NODE_OPTIONS=--use-system-ca`.
- Add production Docker Compose file, `.env.example`, Linux/Mac mini deployment guide, and PostgreSQL backup/restore notes.
- Add responsive QA pass for desktop, tablet, and mobile dashboard layouts.
- Finalize Docker, Linux, and Mac mini deployment notes.
