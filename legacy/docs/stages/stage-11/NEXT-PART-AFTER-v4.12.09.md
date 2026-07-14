# Next Part after v4.12.09

## Stage 11D-95 — Vyapar Import Live File Test Fixes + Customer/Payment Mapping QA

Next stage should be done after deploying v4.12.09 and testing both real Vyapar sale Excel files.

Focus:

1. Fix any remaining build/runtime errors from live deployment.
2. Verify preview grouping for both Vyapar sale files.
3. Verify customer extraction from `Cash Sale(Customer Name)`.
4. Verify customer uniqueness by mobile number.
5. Verify bank/POS/UPI mapping creates correct non-cash payment rows.
6. Verify already imported invoices are hidden on reimport.
7. Verify batch undo cancels imported invoices and reverses stock/accounting.
8. Verify Sale Import pages continue to open inside normal sidebar/navigation.
