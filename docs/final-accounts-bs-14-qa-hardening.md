# Final Accounts BS-14 QA And Hardening

BS-14 is a validation and hardening pass for the isolated Final Accounts module. It does not deploy, run production migrations, edit production host configuration, or enable the `FINAL_ACCOUNTS` flag by default.

## Automated Evidence

Run these commands from `C:\AIarea\Codex\bsheet\GarmetixWebStarter`:

- `dotnet build backend\Garmetix.Api\Garmetix.Api.csproj -c Release -p:UseSharedCompilation=false`
- `dotnet test backend\Garmetix.Api.Tests\Garmetix.Api.Tests.csproj -c Release -p:UseSharedCompilation=false`
- `npm --prefix frontend\modular run final-accounts:readiness`
- `npm --prefix frontend\modular run check`
- `npm --prefix frontend\modular run workspace-links`
- `npm --prefix frontend\modular run main:backoffice-contract`
- `npm --prefix frontend\modular run build:final-accounts`
- `npm --prefix frontend\modular run validate`
- `git diff --check`

## Migration Review

The Final Accounts migration `Up` paths are additive. BS-14 review blocks `DropTable`, `DropColumn`, `RenameTable`, and `RenameColumn` in forward migration paths. Reviewed raw SQL is allowed for scoped unique indexes where the migration helper builds PostgreSQL-safe index statements. EF-generated `Down` paths contain rollback drops for the new `final_accounts` tables only; do not run rollback against production without a separately approved rollback plan. Clean and existing DB migration execution still requires a non-production database supplied by the reviewer.

## Stress And Regression Checks

Large ledger pagination is capped by `FinalAccountsReportRules.MaxPageSize`. Backfill resume checkpoints are stable across module order and date windows. Large exports above 25 MB are flagged for streaming or package review. Concurrent posting retries must use the same normalized idempotency key. Feature-disabled endpoints must return HTTP 403 with `/final-accounts/setup` guidance.

## Browser And Accessibility QA

Manual browser QA covers the dashboard, setup, chart of accounts, fiscal periods, general ledger, posting rules, reports, CA workspace, closeout, projections, and exchange routes. Basic keyboard QA covers dashboard, general ledger, reports, CA workspace, closeout, and exchange workflows with tab order, focus visibility, and no overlapping controls.

## Known Limitations

No production deployment was triggered. No production migration was run. Clean and existing DB migration checks are documented for staging/non-production execution because this workspace does not provide a disposable PostgreSQL target for destructive-free migration rehearsal.
