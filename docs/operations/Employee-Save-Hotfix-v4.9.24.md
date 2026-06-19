# Employee Save Hotfix v4.9.24

Stage 8I Package 23B fixes a runtime failure reported during Package 22/23 HR employee editing.

## Fixed

- `PUT /api/employees/{id}` no longer uses `DefaultIfEmpty(0).MaxAsync(...)` in the employee sequence lookup.
- Employee save now uses a nullable `MaxAsync` projection that PostgreSQL/Npgsql can translate.
- Existing employee edits with an already assigned `EmpId` do not run the auto-sequence query.
- Deprecated `lucide-vue-next` was removed from frontend package metadata because Nuxt UI icons are already provided through `@iconify-json/lucide`.

## Notes

The PAN validation message is intentional validation: PAN is optional, but when entered it must be exactly 10 characters.
