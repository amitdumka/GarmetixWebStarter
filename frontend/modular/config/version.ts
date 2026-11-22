export const garmetixModularVersion = {
  version: '6.9.46',
  stage: 'CRM customer edit fix + Walk-in Customer (0000000000) for sales without a mobile number',
  label: 'CRM Edit Customer loads the record; no-mobile sales use the standard Walk-in Customer instead of blank-mobile customers',
  summary: 'CRM Edit Customer showed a blank New Customer form: CustomerForm used type-only defineProps, which compiled to no props in the production build, so customer-id fell through as an HTML attribute. Switched to runtime props (same fix applied to the new POS print layout picker). Sales: a blank, WALKIN or all-zero mobile no longer creates or reuses a blank-mobile customer. It books to one standard Walk-in Customer with mobile 0000000000, the shared customer is never renamed, and the name typed at the counter is stored on the invoice (CustomerName plus a Customer: remark); a GSTIN typed for a no-mobile B2B bill is kept on the invoice. The billing import follows the same rule.'
} as const
