# Stage 8I Package 5 - Aadwika Fashion + Smart Menswear Merge Utility (v4.9.4)

This package adds an admin-only tool to clean live/existing data after the seeder structure was updated.

## Target structure

```text
Company: Aadwika Fashion
  Store Group: Aadwika Fashion MBO
    Store: Aadwika Fashion MBO Dumka
    Store: Smart Menswear
```

`Aadwika Fashion - Shalini` remains separate and is excluded.

## Added APIs

- `GET /api/company-merge/af-smart/preview`
- `POST /api/company-merge/af-smart/apply`

## Added UI

Open:

```text
Admin → AF/SS Seeder → Aadwika + Smart Menswear Merge
```

Use **Preview merge** first. Apply only after backup.

## What apply does

- Ensures target company `Aadwika Fashion`.
- Ensures target store group `Aadwika Fashion MBO`.
- Ensures/updates store `Smart Menswear`.
- Moves scoped `CompanyId` rows from Smart/Samrat company to Aadwika Fashion.
- Moves scoped `StoreGroupId` rows from Smart/Samrat group to Aadwika Fashion MBO.
- Marks old Smart/Samrat company/group as deleted/inactive.
