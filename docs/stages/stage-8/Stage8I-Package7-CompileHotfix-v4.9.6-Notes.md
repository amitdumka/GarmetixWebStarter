# Stage 8I Package 7 - Seeder Compile Hotfix (v4.9.6)

This hotfix fixes the Docker API build errors reported from `dotnet publish`.

## Fixed

### PortableSeederEndpoints.cs

Added missing helper methods:

- `TryGetGuid`
- `NormalizeLedgerGroupReferenceAsync`
- `SeedDefaultsForAllCompaniesAsync`

Also added `System.Text.Json.Nodes` import and fixed nullable column list warning.

### CompanyMergeEndpoints.cs

Removed invalid `CreatedBy` assignments from:

- `Company`
- `StoreGroup`
- `Store`

These models inherit `BaseEntity`, not `CompanyBase`, so they do not have `CreatedBy`.

### AfssDefaultSeederService.cs

Removed invalid `CreatedBy = "AFSSSeeder"` from `Company` creation.

## Notes

`#18 CANCELED` in Docker is not the real error. It means another Docker build step was cancelled after the API build failed. The real failure was the API compile errors above.
