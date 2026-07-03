# @garmetix/shared-utils

Shared utility helpers for the Version5 modular frontends.

This package is for framework-light helpers such as date formatting, Indian currency formatting, safe message cleanup, string normalization, and small validation helpers.

Rules:

- Keep functions deterministic and easy to test.
- Do not import Nuxt/Vue-only APIs here.
- Keep user-facing error messages free from server URLs; detailed diagnostics belong in message logs.

