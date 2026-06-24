# Stage 13A.5 - Visual Smoke Notes

Version: 5.13.5

## Goal

Close the Stage 13A production hardening lane with repeatable visual smoke notes for the modular apps.

## Added Commands

From the repository root:

```powershell
npm.cmd run modular:smoke:visual
npm.cmd run modular:smoke:visual -- --app=pos
npm.cmd run modular:smoke:visual -- --mode=public --write
```

From the `modular/` folder:

```powershell
npm.cmd run smoke:visual
```

## Coverage

The visual smoke notes cover:

- Login page layout and safe error messages.
- Authenticated shell layout.
- App switching links for local and public modes.
- Access-denied behavior or safe fallback pages.
- 14 inch laptop, desktop and mobile viewport review.

## Generated Notes

Use `--write` to create a local markdown checklist under:

```text
modular/docs/generated/
```

That folder is ignored by git.

## Acceptance

- Visual smoke notes generate for all apps.
- App filtering works with `--app=main|pos|hr|ai-sense|books|admin`.
- Public and local modes generate the correct URLs.
- `modular:validate` includes the visual notes generator.
- No screenshot evidence, credentials or production secrets are committed.

## Stage 13A Closure

Stage 13A now has:

- Route smoke dry-runs and optional live browser checks.
- API/auth smoke dry-runs and optional live network checks.
- Public URL smoke reports for Cloudflare-facing hosts.
- Visual smoke notes for login, shell, app switching and access-denied states.

## Next Step

Stage 13B should start POS parity and writable workflow hardening.
