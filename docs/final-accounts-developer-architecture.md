# Final Accounts Developer Architecture

Final Accounts is an isolated module mounted at `/final-accounts` and `/api/final-accounts`. It must stay feature-flagged by `FINAL_ACCOUNTS` and must not change existing POS, HR, Books, or Accounting routes unless a later merge plan explicitly approves it.

## Backend Shape

The API module lives under `backend/Garmetix.Api/FinalAccounts`. Endpoints use `GarmetixPolicies.FinalAccounts` and the `FinalAccountsEnabledFilter`. Services resolve workspace scope from the request context and query only scoped rows.

## Data Rules

Posted journals are append-only. Corrections use reversals. Source posting is idempotent by source type, source ID, and idempotency key. Period close creates snapshots and lock evidence; reopen is audited and must not delete journals.

## Frontend Shape

The Nuxt app lives under `frontend/modular/apps/final-accounts`. Routes are hidden until the feature flag is enabled. The app should consume only `/api/final-accounts` endpoints.

## Security Rules

Use `FinalAccountsSecurityRules` for the documented permission matrix, scoped not-found wording, audit coverage, and attachment validation policy. Tests must cover tenant isolation, cross-company access, ID enumeration, audit events, and attachment restrictions.
