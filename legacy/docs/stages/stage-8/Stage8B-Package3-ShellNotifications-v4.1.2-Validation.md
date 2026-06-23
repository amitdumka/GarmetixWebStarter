# Stage 8B Package 3 Validation

Date: 2026-06-14

- [x] Backend Release build passes with zero warnings and zero errors.
- [x] Nuxt production build passes; known external font certificate and sourcemap warnings remain tracked separately.
- [x] Existing backend role tests pass: 10 passed, 0 failed.
- [x] Docker API, web, and PostgreSQL services run with healthy v4.1.2 identity.
- [x] Admin desktop shell shows scoped notifications, administrator quick actions, active navigation, and collapsed mode.
- [x] Store Manager shell hides Admin routes and technical Message Logs actions.
- [x] Mobile shell opens and closes cleanly without viewport overflow.
- [x] Existing unrelated backup metadata remains untouched.

## Browser Notes

- Admin notifications contain friendly Inventory and Petty Cash summaries and include the administrator-only Logs shortcut.
- Store Manager quick actions include Billing, Vouchers, Inventory, Petty Cash, HR, and Scan, with no Admin or Message Logs destination.
- Store Manager command search returns no result for Company Setup.
- The mobile shell renders its topbar and menu trigger, closes after navigation to Billing, and remains exactly 390 pixels wide at a 390 x 844 viewport.
- The desktop topbar keeps the page title, search, workspace, refresh, notifications, theme, and account controls visible with no horizontal overflow at 1280 pixels.
- The browser was returned to the Admin account with the sidebar expanded after validation.
- Known residual: authenticated pages still report the previously tracked Vue hydration mismatch.
