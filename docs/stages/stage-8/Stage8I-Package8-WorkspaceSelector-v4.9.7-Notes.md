# Stage 8I Package 8 - Workspace Store Persistence + Top Bar Selector (v4.9.7)

This package fixes the practical store selection issue when multiple stores exist.

## Fixed

- Selected store no longer jumps back to first store after page refresh.
- Active company/store group/store is persisted per user/browser session.
- Saved workspace is preferred before server/default first-store fallback.
- Workspace initialization no longer overwrites selected store while options are still loading.

## Added

- Top bar workspace pill is clickable.
- Top bar shows selected store name.
- One modal to change:
  - Company
  - Store Group
  - Store
- "Set as default" button saves current workspace as default on this browser.
- Small-screen layout can change workspace from the same top bar selector.

## Usage

Click the top bar workspace/store name, choose Company → Store Group → Store, then click **Use workspace**. Click **Set as default** to keep the selection for next refresh/login on this browser.
