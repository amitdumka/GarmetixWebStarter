export const garmetixModularVersion = {
  version: '6.9.47',
  stage: 'Sale invoice: real QR in every format + Instagram QR on thermal receipts',
  label: 'Sale invoices print a scannable invoice QR (A4, A5, thermal) and thermal receipts end with the Instagram follow QR',
  summary: 'The sale invoice PDF never printed a real QR: its canvas Qr() drew an empty box labelled SCAN CODE (the purchase, voucher and petty-cash PDFs already used the real renderer). It now uses the shared DocumentCodeService renderer, so A4 and A5 carry a working GMX:SALE code, and the thermal 58/80 mm receipts, which had only a Scan text line, now print the invoice QR followed by an Instagram follow QR (https://www.instagram.com/aadwikafashion) at the end. POS Sales History now resolves a scanned GMX:SALE code through /api/scan to the invoice number and widens the date range to all sales so older invoices are found. Added regression tests that fail if a QR is ever drawn as an empty box again.'
} as const
