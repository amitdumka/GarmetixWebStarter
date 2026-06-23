# @garmetix/shared-types

Shared TypeScript contracts for the Version5 modular frontends.

This package owns app IDs, route ownership metadata, user/session types, workspace context, and common DTO shapes that multiple apps need.

Rules:

- Keep types framework-light where possible.
- Prefer shared contracts over copied inline page types.
- Keep app ownership explicit so menus and permissions can be generated consistently.

