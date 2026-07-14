# Stage 8I Package 6 - Seeder Syntax Hotfix + Verification (v4.9.5)

## Fixed

AF/SS seeder comparison list had a missing comma in `Seeder2CsOnly`.

Fixed section:

```csharp
Seeder2CsOnly:
[
    "Async seeding flow with StringBuilder messages and per-section exception handling.",
    "Smart Menswear profile merges into Aadwika Fashion company under Aadwika Fashion MBO store group.",
    "Safer name split logic for owner/employees.",
    "Corrected SGST 2.5% label spelling."
],
```

## Added

New admin-only API:

- `GET /api/seeder-verification/status`

New UI block:

- `Admin → AF/SS Seeder → Seeder/Merge Verification`

It verifies:

- Aadwika Fashion company exists.
- Aadwika Fashion MBO store group exists.
- Aadwika Fashion MBO store exists.
- Smart Menswear store is under Aadwika Fashion MBO.
- Aadwika Fashion - Shalini remains separate.
- Protected default accounting masters exist.
- Old Smart/Samrat company rows are cleaned.
