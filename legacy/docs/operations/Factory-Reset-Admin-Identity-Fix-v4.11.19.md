# Factory Reset Admin Identity Fix v4.11.19

## Problem

`POST /api/factory-reset` returned HTTP 400 with:

> The current administrator identity could not be verified.

The Admin authorization policy allowed the request, but the endpoint then checked only the raw JWT `sub` claim. Depending on JWT bearer claim mapping, the same user id may be available as `ClaimTypes.NameIdentifier` instead of `sub`.

## Fix

Factory reset now resolves the current user id from multiple supported claim names:

- `ClaimTypes.NameIdentifier`
- `sub`
- `nameid`
- XML nameidentifier claim URI

The endpoint then validates that the user still exists, is active, and is an admin or super admin before creating the safety backup and truncating business data.

## Version

- Version: `4.11.19`
- Build: `GARMETIX-11D4-20260624-4119`
- Stage: `Stage 11D-4 Factory Reset Admin Identity Fix`
