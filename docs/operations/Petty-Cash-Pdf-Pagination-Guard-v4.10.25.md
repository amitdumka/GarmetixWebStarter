# Petty Cash PDF Pagination Guard v4.10.25

This package fixes the Petty Cash server PDF detail section so transaction rows are never dropped because of A5 page limits.

## Protected Behavior

- The first A5 landscape page remains the colored daily Petty Cash summary with QR scan code.
- Transaction details are printed on additional A5 pages.
- Detail pages paginate with `Page X of Y` instead of showing a "not printed" warning.
- The final detail page shows income, expense and adjustment totals.
- The Petty Cash page still opens server PDF printing after a new sheet is saved.
- Opening balance is calculated from the previous day's closing/cash-in-hand value.
- The dashboard widget shows latest cash in hand, not a total of every sheet.
- Mismatches continue to log owner alerts and expose reconciliation differences in the print dialog.

## Validation

```bash
python scripts/validation/petty-cash-acceptance-check.py
python scripts/validation/current-release-checks.py
```
