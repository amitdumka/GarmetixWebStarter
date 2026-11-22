export const garmetixModularVersion = {
  version: '6.9.49',
  stage: 'Component props fix, receipt policy link, duplicate customer merge',
  label: 'Fixes components whose props were silently dropped in production builds; receipt policy link no longer prints the LAN address; duplicate customers can be merged',
  summary: 'Every component declared its props with the type-only defineProps form, which this production build compiles to zero props (the CRM Edit Customer blank form was one case). All 16 remaining components now use runtime prop declarations, which also revives the POS toast, the placeholder pages, the CRM and Main read-only tables and the Debit/Credit note registers. The goods return policy link printed on invoices and digital bills now falls back to the public domain when the request came in over a raw IP address, localhost or a LAN name. Admins can merge two customers that share the same mobile number through a new preview-then-confirm endpoint.'
} as const
