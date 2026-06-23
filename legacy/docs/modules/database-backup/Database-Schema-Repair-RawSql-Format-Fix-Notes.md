# Database Schema Repair Raw SQL Format Fix

## Issue

Docker logs showed:

```text
System.FormatException: Input string was not in a correct format. Failure to parse near offset 582. Expected an ASCII digit.
```

The failure occurred inside `DatabaseSchemaRepairService.RepairGstReturnStorageAsync` before the repair could create missing tables such as `GstReturnDrafts` and `CommercialNotes`.

## Root cause

`ExecuteSqlRawAsync` internally treats `{...}` as string-format placeholders. The repair SQL used PostgreSQL defaults like:

```sql
DEFAULT '{}'
```

That literal JSON object was parsed as a .NET format item and caused `FormatException`.

## Fix

Escaped literal braces in the SQL string:

```sql
DEFAULT '{{}}'
```

EF sends this to PostgreSQL as:

```sql
DEFAULT '{}'
```

## Result

The idempotent schema repair can now create/repair:

- `GstReturnDrafts`
- `GstReturnAuditEntries`
- `CommercialNotes`
- `CustomerAdvanceReceipts`
- `LoyaltyPrograms`
- `LoyaltyPointLedgers`

Run with a no-cache API rebuild to ensure Docker uses the patched API image.
