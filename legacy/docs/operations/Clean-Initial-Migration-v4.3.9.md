# Clean Initial Migration v4.3.9

This package removes historical incremental EF Core migrations and replaces them with a single fresh schema baseline for new production installations.

## Why

The previous migration chain contained historical branch migrations that assumed older tables already existed, for example `NonGstGoodsDocuments` and `PurchasePayments`. On a fresh PostgreSQL database this caused API startup failures during `Database.Migrate()`.

## New behavior

- Historical migration files were removed from `backend/Garmetix.Infrastructure/Data/Migrations`.
- A single baseline migration marker is kept: `20260617000000_InitialFreshSchema`.
- Startup uses `Database:SchemaBootstrapMode=FreshBaseline` by default in Docker production.
- In FreshBaseline mode, EF Core creates the schema from the current `GarmetixDbContext` model using `EnsureCreated()`, then writes the baseline row to `__EFMigrationsHistory`.
- Future migrations can be generated from the current `GarmetixDbContextModelSnapshot.cs`.

## Important

This is intended for a clean/fresh install. Do not use the database reset option on a production database that contains real data.

## Historical migration files removed

- `20260604101201_InitialCreate.Designer.cs`
- `20260604101201_InitialCreate.cs`
- `20260604103426_AddUserLoginIndexes.Designer.cs`
- `20260604103426_AddUserLoginIndexes.cs`
- `20260604105945_AddSetupAndLocalBusinessTimestamps.Designer.cs`
- `20260604105945_AddSetupAndLocalBusinessTimestamps.cs`
- `20260605062256_WidenEmployeeAadhar.Designer.cs`
- `20260605062256_WidenEmployeeAadhar.cs`
- `20260605072507_AddAccountingJournalAndBanking.Designer.cs`
- `20260605072507_AddAccountingJournalAndBanking.cs`
- `20260606100212_AddVoucherCashTransaction.Designer.cs`
- `20260606100212_AddVoucherCashTransaction.cs`
- `20260606102534_SeparateOffBookCashVouchers.Designer.cs`
- `20260606102534_SeparateOffBookCashVouchers.cs`
- `20260606211500_AddPasswordResetTokens.cs`
- `20260606223000_AddGstReturnDrafts.cs`
- `20260606224500_AddPartyGstinVerification.cs`
- `20260607064000_AddCommercialNotesAdvancesLoyalty.cs`
- `20260608093000_AddStage3AProductMaster.cs`
- `20260608100000_AddStage3BBillingCustomerPaymentUi.cs`
- `20260609173500_ConsolidateStage3To5Schema.cs`
- `20260610114000_AddNonGstGoodsModule.cs`
- `20260610125000_EnhanceNonGstGoodsMemoReports.cs`
- `20260614094500_SeparateNonGstGoodsFromBooks.cs`
- `20260615014500_AddFormalPurchaseReturns.cs`
- `20260615043000_AddPurchaseReturnPrintAudit.cs`
- `20260615061000_AddVendorSettlements.cs`
- `20260615083000_AddPurchaseReturnItcReconciliation.cs`
- `20260615102000_AddFormalStockOperationDocuments.cs`
- `20260615143000_AddWeightedAverageStockValuation.cs`
- `20260615150000_BackfillLegacyStockProjectionMovements.cs`
- `20260615173000_AddStockOperationAccounting.cs`
- `20260615193000_HardenDocumentSequenceConcurrency.cs`
- `20260616090000_AddCashVoucherConversionAudit.cs`
- `20260616133000_AddInvoicePaymentDetails.cs`
