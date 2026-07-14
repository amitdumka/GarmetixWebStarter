# Stage 8I Package 23B - Employee Save Hotfix v4.9.24

## Purpose

Patch the Package 22/23 HR Employee Master save workflow after a production error showed EF Core could not translate the employee auto-sequence query during `PUT /api/employees/{id}`.

## Changes

- Replaced the non-translatable `DefaultIfEmpty(0).MaxAsync(...)` employee sequence expression with nullable `MaxAsync`.
- Runs the employee sequence lookup only when `EmpId` is missing or zero.
- Keeps existing employee `EmpId` during edits.
- Removed deprecated `lucide-vue-next` dependency from frontend package metadata.
- Added static validation for this regression.

## Release metadata

- Version: `4.9.24`
- Build code: `GARMETIX-8I-20260619-49240`
- Stage: `Stage 8I Package 23B Employee Save Hotfix`
