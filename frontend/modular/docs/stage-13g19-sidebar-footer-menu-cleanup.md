# Stage 13G.19 Sidebar Footer Menu Cleanup

Version: 5.13.59

## Purpose

Clean up the modular Nuxt UI dashboard shell footer so utility items do not overlap the main sidebar navigation and Status remains focused on live application state.

## Changes

- Moved Status and Notifications into the real sidebar footer stack.
- Kept the Status submenu limited to current workspace, clock, API state and running version/stage.
- Moved operational routes such as System Health, Runtime Diagnostics, Backup Maintenance, Message Logs and About Version into the footer user menu under System Tools.
- Changed sidebar navigation groups so only the group for the current route opens by default instead of opening every module section at once.
- Added footer spacing styles to prevent Status, Notifications and user controls from colliding with the menu body.

## Validation

- Run `npm run check`.
- Build the modular apps because the shared shell is used by every app.
- Browser-check the sidebar footer on at least one nested app route such as `/hr/` and one admin route such as `/admin/system-health/`.

## Remaining Notes

- Individual page interiors still need the Option A hybrid dashboard polish planned for the next stage.
