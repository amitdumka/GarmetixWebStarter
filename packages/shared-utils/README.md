# Shared Utils Package

Purpose: small dependency-free utilities shared across Garmetix frontend apps.

Planned extraction source:

- Date formatting currently repeated in pages.
- Money and quantity formatting currently repeated in pages.
- Safe message/string formatting from `useUiFeedback.ts`.

Responsibilities:

- Keep formatting consistent.
- Avoid copying helper code between modular apps.
- Stay framework-neutral where possible.

