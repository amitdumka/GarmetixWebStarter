# GSTIN Verification Notes

This module adds a production-shaped GSTIN lookup and party validation flow for Customers and Vendors.

## What it does

- Validates GSTIN format locally.
- Calls a configurable GSTIN lookup provider when `GstinLookup:Enabled=true`.
- Extracts common provider fields such as legal name, trade name, status, taxpayer type, and principal address.
- Compares entered party name/address against the fetched GSTIN record.
- Stores verification data on `Customer` and `Vendor` records.
- Keeps mismatch warnings on the party record through `GSTMismatchAlert`.

## New endpoints

```text
GET  /api/gstin/{gstin}
POST /api/gstin/lookup
POST /api/gstin/validate-party
```

## Stored fields on Customer and Vendor

```text
GSTLegalName
GSTTradeName
GSTPrincipalAddress
GSTStateCode
GSTTaxpayerType
GSTRegistrationStatus
GSTVerified
GSTVerifiedAt
GSTLookupSource
GSTMismatchAlert
```

## Configuration

Use a licensed/approved GSTIN data provider and configure it in `.env` or appsettings.

```env
GSTIN_LOOKUP_ENABLED=true
GSTIN_LOOKUP_BASE_URL=https://provider.example.com/gstin
GSTIN_LOOKUP_URL_TEMPLATE=
GSTIN_LOOKUP_API_KEY=your-provider-api-key
GSTIN_LOOKUP_API_KEY_HEADER=x-api-key
GSTIN_LOOKUP_SOURCE_NAME="Your Provider Name"
```

`GSTIN_LOOKUP_URL_TEMPLATE` is optional. Use it when your provider needs a custom URL shape, for example:

```env
GSTIN_LOOKUP_URL_TEMPLATE=https://provider.example.com/api/search?gstin={gstin}
```

When disabled, the app still checks GSTIN format and state code, but it cannot fetch legal name/address.

## UI changes

- New `/parties` page for Customer/Vendor creation with GSTIN check.
- Billing invoice form has Customer GSTIN check.
- Purchase inward form has Vendor GSTIN check.
- Alerts are shown before saving and also stored on the party record.

## Database migration

Run this before testing the new stored fields:

```bash
dotnet ef database update --project backend/Garmetix.Infrastructure --startup-project backend/Garmetix.Api
```

The migration is:

```text
20260606224500_AddPartyGstinVerification
```
