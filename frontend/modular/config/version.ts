export const garmetixModularVersion = {
  version: '6.0.68',
  stage: 'Stage 14O Debit/Credit/Commercial Notes Implementation',
  label: 'Version6 Stage 14O Debit/Credit/Commercial Notes Implementation',
  summary: 'Books: implemented Debit Notes, Credit Notes and Commercial Notes, which were routed and scaffolded but only ever rendered a BooksPlaceholder stub (never actually built). The backend commercial-notes API (list/get/create/update/pdf/mark-printed, unified by NoteType) already existed and was unused by the frontend. Added a shared CommercialNoteEntryForm.vue component (party type/party picker, amount/tax fields, reason/remarks, A4/A5 PDF download) reused by both /debit-notes/new + /debit-notes/{id} and /credit-notes/new + /credit-notes/{id}, ported from the legacy CommercialNoteEntryForm.vue reference with the same field set and NoteType/PartyType enum values. Rebuilt /debit-notes and /credit-notes as real registers (BooksMasterTable, search, PDF actions) and /commercial-notes as a combined debit+credit+customer-advances summary register, matching legacy feature parity.'
} as const
