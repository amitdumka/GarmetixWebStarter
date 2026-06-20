# Stage 10G Navigation Menu Coverage v4.10.14

## Goal

Ensure every concrete Nuxt page is discoverable from the sidebar/menu so users do not lose access to pages after Stage 9 and Stage 10 additions.

## Changes

- Added direct modern sidebar links for:
  - `/billing/new` — New Sale Invoice
  - `/customers/new` — New Customer
  - `/debit-notes/new` — New Debit Note
  - `/credit-notes/new` — New Credit Note
- Added Account and Help menu groups to sidebar coverage:
  - `/profile`
  - `/about-us`
  - `/contact-us`
  - `/faq`
- Updated the legacy sidebar to mirror the modern sidebar, so deployments using `DASHBOARD_SHELL=legacy` still show Stage 9 and Stage 10 pages.
- Added `scripts/validation/navigation-menu-coverage-check.py`.
- Added the navigation menu coverage check into `scripts/validation/current-release-checks.py`.

## Approved exception

`/access-denied` is intentionally not shown in the menu because it is an auth/guard destination.

Dynamic detail/edit routes such as `/customers/[id]`, `/credit-notes/[id]`, and `/debit-notes/[id]` are not direct menu entries because they are opened from their parent list pages.

## Validation

```bash
python3 scripts/validation/navigation-menu-coverage-check.py
python3 scripts/validation/current-release-checks.py
```
