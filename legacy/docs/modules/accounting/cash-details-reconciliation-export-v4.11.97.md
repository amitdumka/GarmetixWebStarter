# v4.11.97 Cash Details Reconciliation Export

Stage 11D-82 improves the Cash Details module so store cash denominations can be verified, copied, exported and compared with Day Opening/Closing and petty cash sheet values.

## Backend

Added endpoint:

```text
GET /api/cash-details/day-check?storeId={storeId}&onDate={yyyy-MM-dd}
```

It returns:

- Day opening balance and linked opening cash-detail id.
- Day closing balance and linked closing cash-detail id.
- Petty cash sheet cash-in-hand.
- Latest counted physical cash.
- Variance between latest physical cash and opening/closing/petty cash sheet values.
- Count of cash-detail rows for the selected day.
- Full cash denomination rows for the selected day.

## Frontend

Page changed:

```text
frontend/garmetix-web/pages/cash-details/index.vue
```

Added:

- Day Cash Reconciliation card.
- Variance warning when latest counted cash differs from day opening, day closing or petty cash sheet.
- Copy latest cash detail as a new `CashVerification` row.
- Denomination breakdown table in the add/edit form.
- Counted pieces metric.
- Amount override variance warning.
- Export CSV for the current filtered list.
- Quick buttons: Today, Zero notes, Clear.
- Copy action for any history row.

## Business behavior

- Linked Day Opening/Closing records remain protected from delete.
- Editing linked cash detail still syncs opening/closing amount and petty cash sheet cash-in-hand where applicable.
- CSV export is client-side and uses the current filtered rows.

## Test checklist

1. Open Cash Details.
2. Select store and date.
3. Verify Day Cash Reconciliation card loads.
4. Add manual cash denominations and save.
5. Copy row as CashVerification and save.
6. Confirm variance warning appears if amount differs from Day Opening/Closing.
7. Export CSV and open file.
8. Edit a Day Closing linked cash detail and verify petty cash sheet cash-in-hand updates.
