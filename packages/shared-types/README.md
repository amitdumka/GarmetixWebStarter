# Shared Types Package

Purpose: shared TypeScript contracts used across Garmetix frontend apps.

Planned extraction source:

- Auth/session DTOs from `useAuth.ts`.
- Route/menu/permission types from `AppShell.vue` and `useAccessControl.ts`.
- Common API response shapes from frontend pages and backend DTOs.

Responsibilities:

- Keep route metadata consistent.
- Keep user/session contracts consistent.
- Avoid duplicating DTO shapes across POS, HR, Books, AI Sense, Admin/SaaS, and main-web.

