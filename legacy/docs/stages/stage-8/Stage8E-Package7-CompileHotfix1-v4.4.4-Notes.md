# Stage 8E Package 7 Compile Hotfix 1 - v4.4.4

This hotfix resolves two Docker publish compile issues found during Mac mini deployment.

## Fixes

- Removed invalid `Vendor.VendorType` usage from tailoring vendor lookup. The current Vendor master does not have a `VendorType` property, so tailoring now lists active vendors and labels them as `Vendor`.
- Removed duplicate migration attributes from `20260617000000_InitialFreshSchema.cs`. The EF Core designer file owns `[DbContext]` and `[Migration]`; the main migration class is now a plain partial `Migration` class.

## Deployment note

Use the normal WSL deploy script. This package preserves `RESET_DATABASE_ON_DEPLOY=false`.
