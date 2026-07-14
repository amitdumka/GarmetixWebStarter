# Stage 7M — Pre-v4.0 UI Naming and Menu Cleanup

Base package: Stage 7L v3.11.1 buildfix.

## Purpose

This package handles the requested pre-Stage 8 cleanup before moving the project to v4.0. It focuses on UI naming, page header size, menu structure, visible stage-label removal, login copy cleanup and dashboard context naming.

## Implemented

### Dashboard titles and heading size

- Reduced dashboard hero heading size from large marketing-style text to a normal application page-title size.
- Reduced dashboard hero padding so the header takes less vertical space.
- Store dashboard now uses the current store name as the title when available.
- Company dashboard now uses the current company name as the title when available.
- Store Manager dashboard naming was changed to Store dashboard in menus/context.
- Owner/Admin dashboard naming was changed to Company dashboard in menus/context.

### Visible stage-label cleanup

- Removed visible internal stage numbers from page headers and badges.
- Replaced visible stage badges with context names such as Product master, Stock operations, GST reports, Message logs, Client onboarding, Non-GST goods and System info.
- About Us now shows Release instead of Stage in the visible version identity table.

### Sidebar/menu reorganization

Reports and GST were removed from the Dashboards group.

New sidebar groups:

- Dashboards
- Sales
- Purchase
- Inventory
- Accounting
- CRM
- GST
- Reports
- Off Book
- People
- Admin
- Data
- Maintenance
- System

Important moves:

- Reports moved to Reports.
- GST Returns and GST Reports moved to GST.
- Non-GST Goods moved to Off Book.
- Billing and Sales Return moved to Sales.
- Purchase and Purchase Return moved to Purchase.
- Inventory and Stock Operations moved to Inventory.
- Vouchers, Debit Notes, Credit Notes, Commercial Summary and Petty Cash moved to Accounting.
- Customers, Parties & Vendors and Loyalty moved to CRM.
- Admin tools split across Admin, Data, Maintenance and System.

### Sidebar behavior

- Sidebar menu groups now default-open only when their route is active.
- Navigation menus remount per route so the selected route controls which group is open.
- Footer button renamed from Status & Workspace to Status.
- Sidebar brand subtitle now shows only the version number.

### Login page cleanup

- Changed Garmetix Web to Garmetix.
- Removed JWT/session technical helper badges from the login page.
- Removed login-mode helper text under Login to Garmetix.
- Kept forgot password and reset token actions as small link-style buttons.

### Version identity

- Version: 3.12.0
- Stage: Stage 7M
- Release: Pre-v4.0 UI Naming and Menu Cleanup
- Build Code: GARMETIX-7M-20260611-3120

## Not changed

- No existing page was removed.
- Legacy shell revert remains available with `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`.
- Backend dashboard API contracts remain backward compatible.
- Stage 7L Docker/Nuxt heap buildfix remains in the frontend Dockerfile.

## Before Stage 8 / v4.0

Use `/ui-audit` to do a page-by-page visual pass for margins, padding, gap, table overflow, button wrapping, modal spacing, mobile layout and overlap with sidebar/topbar/footer surfaces.
