export const garmetixModularVersion = {
  version: '6.9.45',
  stage: 'POS: saved print layout (A4 / A5 / Thermal 58 mm / Thermal 80 mm)',
  label: 'POS invoices print in the layout chosen on this device instead of always A4',
  summary: 'POS always sent invoices to the normal A4 printer layout (Save and Print, Sales History print/reprint, Print Queue). The API already renders thermal-2 (58 mm) and thermal-3 (80 mm) PDFs, but no page ever asked for them. Added a per-device saved Print layout setting (localStorage) with a picker on Sale, Sales History (header and View panel) and Print Queue; openBillingInvoicePdf now defaults to the saved layout, so every print path (sale, history, print queue, returns, exchange, returns register) follows it with A4 as the default.'
} as const
