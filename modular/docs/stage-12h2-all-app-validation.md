# Stage 12H.2 - All-App Validation

Version: 5.12.32

## Goal

Add one local command that validates the Version5 modular workspace before deployment.

## Commands

From the repository root:

```powershell
npm.cmd run modular:validate
```

From the modular folder:

```powershell
npm.cmd run validate
```

## What It Runs

1. Modular structure validation.
2. Main Back Office static build.
3. POS static build.
4. HR static build.
5. AI Sense static build.
6. Books static build.
7. Admin/SaaS static build.
8. Shared ASP.NET API release build.

## Optional Flags

For faster local checks:

```powershell
node modular/scripts/validate-all.mjs --skip-builds
node modular/scripts/validate-all.mjs --skip-api
```

Use the full command before a deploy or before pushing a stage that affects shared packages, routing, auth, environment configuration, or deploy scripts.

## Notes

- The script keeps the one-backend and one-database architecture unchanged.
- The script does not deploy anything.
- The script does not require production secrets.
- Frontend builds may still show external font provider certificate warnings on some Windows environments; the validation fails only when a command exits non-zero.

## Next Step

Stage 12H.3 should add a safer deployment preflight checklist and remote host readiness check that does not store passwords or secrets.
