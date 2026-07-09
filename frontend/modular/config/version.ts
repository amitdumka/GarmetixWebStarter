export const garmetixModularVersion = {
  version: '6.0.67',
  stage: 'Stage 14N.2 Books Home Dashboard 404 Fix',
  label: 'Version6 Stage 14N.2 Books Home Dashboard 404 Fix',
  summary: 'Books: fixed the Books home dashboard (base_url/books) calling a nonexistent GET api/gst/reports endpoint on every load, which 404d silently (surfaced only as a generic "Books summaries could not be loaded yet" banner, or a raw 404 if checked directly). Replaced with the real gst-returns/drafts listing endpoint (already used successfully by gst-returns.vue) and renamed the dashboard metric card from "GST Rows" to "GST Drafts" to match. Also confirmed the Voucher entry form Ledger searchable-picker field was never removed from source - the "removed" report was the 6.0.65 BooksMasterTable init-crash (fixed in 6.0.66) blanking the whole page, not an actual missing field.'
} as const
