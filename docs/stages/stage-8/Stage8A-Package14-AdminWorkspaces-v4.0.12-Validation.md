# Stage 8A Package 14 Validation

Date: 2026-06-14
Version: 4.0.12

## Completed Checks

- [x] Backend build completed with 0 warnings and 0 errors.
- [x] Nuxt production build completed.
- [x] Docker API and web images rebuilt and containers restarted.
- [x] Runtime health and version endpoints report a healthy v4.0.12 service.
- [x] Company Setup, Onboarding, AF/SS Defaults, and Roles & Users were checked at 1280x720.
- [x] All four Admin pages were checked at 390x844 without viewport overflow.
- [x] Company and user forms open as 1152px desktop modal workspaces.
- [x] User form fits a 358px mobile modal without viewport overflow.
- [x] Roles & Users no-scope selectors open without the Nuxt SelectItem empty-value error.
- [x] Setup date payload uses local `YYYY-MM-DDT00:00:00` serialization without UTC conversion.

## Known Non-Blocking Warnings

- External Nuxt font metadata providers can still report local certificate-chain warnings.
- Existing dependency sourcemap and package export deprecation warnings remain non-blocking.
- The previously tracked authenticated-shell hydration mismatch remains visible and is outside this package.
