# Stage 8A Package 1 - UI Audit and Accounting Registers

Date: 2026-06-13

## Scope

This package begins Stage 8A / v4.0 without changing business routes or API contracts.

## Implemented

- Converted `/ui-audit` from a read-only route list into an actionable review queue.
- Added persistent per-page status: Needs visual pass, In progress, and Reviewed.
- Added persistent review notes, module/status filters, search, summary counts, reset, and direct page navigation.
- Added the shared `UiRegisterPanel` component for consistent loading, error/retry, empty, header/action, and table states.
- Standardized the Credit Note and Debit Note registers as the first Accounting pages in the Stage 8A queue.
- Added register search, refresh, visible error/retry, useful empty states, aligned numeric columns, status badges, PDF action labels, and responsive action wrapping.
- Added Stage 8A responsive CSS guardrails for register headers, toolbars, filters, and mobile stacking.

## Preserved

- Existing credit/debit note API endpoints and document download behavior.
- Dedicated full-page create and edit routes.
- Existing role and route permissions.
- Existing petty-cash print changes in the current worktree.

## Validation

```powershell
python scripts/validation/stage8a-package1-static-checks.py
cd frontend/garmetix-web
npm.cmd run build
```

The Nuxt production build passes. External font-provider metadata warnings remain a separate Priority 0 build-environment item.

## Next Package

Continue the Stage 8A queue with Commercial Notes and Customer registers, then move through the remaining Accounting and CRM pages using the same shared states and responsive register pattern.
