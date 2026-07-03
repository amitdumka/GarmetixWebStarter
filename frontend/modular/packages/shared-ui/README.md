# @garmetix/shared-ui

Shared UI contracts and future reusable Nuxt UI/Vue components for the Version5 modular frontends.

This package currently holds shell navigation contracts. Real components should move here only when two or more modular apps need the same UI pattern.

Rules:

- Keep domain-specific forms inside their app until reuse is real.
- Prefer Nuxt UI v4.9 components for shared UI.
- Do not move legacy components wholesale until the owning app route is being migrated.

