# Stage 14A.6 POS Cashier Workflow Acceptance

Version: 6.0.6

## Purpose

Stage 14A.6 verifies the cashier-facing POS workflows before moving to the next module. It focuses on the sale, return and exchange screens used at the counter on a 14 inch laptop.

## Covered Workflows

- Product barcode/search input is present and wired to Enter.
- Sale payment split is supported.
- Non-cash payment requires a bank account before save.
- Sale save success is separate from print recovery.
- Return invoice search supports scanned QR/document text and Enter.
- Return quantity and refund amount are clamped.
- Non-cash return refund requires a bank account.
- Exchange original invoice search and replacement product scan are wired.
- Exchange additional payment and remaining credit preview are visible.
- Sale, return and exchange all hand saved documents to the print queue.

## Commands

Dry source-contract check:

```powershell
npm.cmd run modular:pos:cashier-workflow
```

Live SRP read-only check:

```powershell
$env:NODE_OPTIONS='--use-system-ca'
npm.cmd run modular:pos:cashier-workflow -- --live
```

Optional browser fixture mode after Playwright is installed:

```powershell
npm.cmd --prefix frontend/modular install --save-dev playwright
npm.cmd --prefix frontend/modular exec playwright install chromium
$env:NODE_OPTIONS='--use-system-ca'
npm.cmd run modular:pos:cashier-workflow -- --live --browser
```

## Safety

The default and live checks are read-only. They do not create sales, returns, exchanges, held bills or printer jobs. Browser fixture mode mocks API responses in the browser and still avoids live database writes.

## Remaining Manual Acceptance

- Physical barcode scanner timing.
- Actual printer popup behavior.
- Real cashier speed test at 100 percent browser zoom on the store laptop.
- Real token-based sale, return and exchange write tests after a fresh backup.

## Next Stage

Stage 14A.7 should close the POS-first lane with final live-token acceptance, manual cashier checklist evidence and a go/no-go decision before HR modular parity work starts.
