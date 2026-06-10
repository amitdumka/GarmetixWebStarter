# Workspace and Store Permission Flow

This update makes the Company / StoreGroup / Store workspace server-aware instead of only filtering in the browser.

## Backend

- Added `GET /api/workspace/options`.
- Response contains only the companies, store groups, and stores allowed for the current logged-in user.
- Response also includes default workspace IDs and lock flags:
  - `defaultCompanyId`
  - `defaultStoreGroupId`
  - `defaultStoreId`
  - `isCompanyLocked`
  - `isStoreGroupLocked`
  - `isStoreLocked`
- Added `WorkspaceScope` helper for server-side filtering and write validation.
- Generic CRUD endpoints now apply workspace scope to list/get/update/delete operations.
- Billing sale creation, invoice list/receipt/cancel, purchase inward, and quick product creation validate the selected store against the logged-in user's scope.

## Frontend

- `AppShell` now loads `workspace/options` for the topbar and mobile workspace modal.
- Company, StoreGroup, and Store dropdowns auto-select the backend default workspace after login.
- Dropdowns lock automatically when the user is assigned to a specific Company, StoreGroup, or Store.
- Access page now lets admins assign Company, StoreGroup, Store, and AppOperation together.

## User setup rules

- Admin / Owner / AppOperation `All`: can see all companies and stores.
- User with `CompanyId`: can see/write only that company data.
- User with `StoreGroupId`: can see/write only that store group data.
- User with `StoreId`: can see/write only that store data.

For best results, assign all three values (`CompanyId`, `StoreGroupId`, `StoreId`) for cashier/store users.
