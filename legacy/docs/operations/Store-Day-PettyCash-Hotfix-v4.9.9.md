# Store Day / Petty Cash Hotfix v4.9.9

## Verify Store Day

1. Open **Store Day Open / Close**.
2. Open a day.
3. Confirm no frontend error appears.
4. Close the day.
5. Confirm no frontend error appears.
6. Print Petty Cash.
7. Confirm PDF has summary page and transaction detail page.

## Reopen wrong day close

Use:

```text
Store Day Open / Close → Reopen Day
```

This removes the active close lock and generated close records so data can be corrected.

## Delete wrong close

Use:

```text
Store Day Open / Close → Delete Close
```

Same correction flow for wrong close operation.

## Verify calculation

Enter receipt through both Voucher and Cash Voucher. Prepare/close Petty Cash and confirm both are counted.
