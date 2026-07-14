# Stage 14D.2a CRM Nuxt UI Layout Hotfix

Version: `6.0.32`

This hotfix repairs the CRM modular frontend layout so it renders with the same Nuxt UI and Tailwind styling foundation as POS and HR.

## Problem

The CRM app had the same Nuxt UI module entry as POS/HR, but its `assets/css/main.css` did not import:

- `tailwindcss`
- `@nuxt/ui`

Because of that, the static CRM site could build successfully but render like an unstyled or partially styled page.

## Fixed

- Added the missing stylesheet imports to `frontend/modular/apps/crm/assets/css/main.css`.
- Aligned CRM auth middleware with POS/HR:
  - expired session cleanup
  - redirect query preservation
  - logged-in `/login` redirect back to the requested path
- Bumped modular version identity to `6.0.32`.

## Validation

- Build CRM app.
- Confirm CRM generated CSS is comparable to other Nuxt UI apps, not the tiny placeholder CSS.
- Run modular structure validation.
- Deploy SRP frontend-only update.
- Check `/crm/`, `/crm/customers/`, and `/crm/customers/new/` publicly.
