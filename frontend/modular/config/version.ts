export const garmetixModularVersion = {
  version: '6.9.51',
  stage: 'Return cancel guard, store credit refund, walk-in credit guard',
  label: 'Sales returns can no longer be cancelled, held store credit can be refunded, walk-in customers cannot hold store credit',
  summary: 'Day Close and Petty Cash now deduct store credit paid out in cash. Cancelling a return or credit note would have put stock back twice, so it is now blocked, as is cancelling an invoice that has returns. Cancelling an exchange now gives the store credit it used back to the customer. The shared Walk-in Customer can no longer be left holding store credit and can only use the credit its own exchange created. A new store credit refund endpoint pays held credit out in cash or by bank mode.'
} as const
