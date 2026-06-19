# Stage 8H Package 9 - Single Scroll Layout Hotfix (v4.7.8)

This package fixes the double-scrollbar problem reported across Store Operation, Stock Report, FY Locks, Loyalty, GST pages, Cash Voucher, Non-GST Goods, AF/SS Seeder, Roles and similar pages.

## Included

- Removed nested vertical scrolling from the dashboard slot wrapper.
- Removed nested vertical scrolling from the legacy dashboard content wrapper.
- Expanded message log lists and role/access matrices naturally instead of keeping separate inner scrollbars.
- Added global page guardrails so regular content cards/registers expand with the main page scroll.
- Kept horizontal table scrolling for wide registers and reports.
- Kept dialogs, sidebars, command search, notifications and receipt overlays scrollable where separate scrolling is expected.
