# Stage 11D-27 / v4.11.42 - Sale Revised Invoice Clone

Adds a safe sale invoice revision workflow, similar to purchase revised inward.

## Flow

1. Open Billing → Sales Invoices.
2. Click Revise on an existing invoice.
3. The app opens `/billing/new?copyFrom=<invoiceId>`.
4. Old customer and item lines are copied into a new invoice draft.
5. User edits item/qty/rate/discount/customer as required.
6. Save creates a new sales invoice. Original invoice is not changed automatically.

## Important note

If the original invoice already consumed the same stock, creating the revised invoice before cancelling the old one may fail for insufficient stock. Use the workflow that matches the situation:

- Minor customer/header correction: use Edit.
- Item correction where enough stock exists: Revise, save new, then Admin/Owner cancels old invoice.
- Item correction where stock is locked by old invoice: Admin/Owner cancels old invoice first, then create revised invoice.
