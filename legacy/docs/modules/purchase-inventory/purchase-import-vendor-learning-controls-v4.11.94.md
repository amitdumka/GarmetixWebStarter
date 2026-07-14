# v4.11.94 - Purchase Import Vendor Learning Controls

This stage adds manual controls for supplier invoice import learning profiles.

## Why

OCR/table extraction improves after users correct drafts, but a wrong correction can also teach the importer a bad rule. This stage makes vendor-wise learning editable so the shop can correct the learning without deleting the whole profile.

## Added

- Add manual ignored row patterns per vendor profile.
- Remove one ignored pattern without resetting all ignored-row learning.
- Remove one product alias mapping without resetting all product aliases.
- Save learning notes describing the vendor's invoice layout, such as Tally Prime, S.K APPARELS column layout, Art No product naming, or GST-exclusive billing.

## Page

Purchase → Import Learning

## API

```http
POST /api/purchase-import/vendor-profiles/{id}/rules
```

Payload:

```json
{
  "addIgnoredLinePatterns": ["Bank Details"],
  "removeIgnoredLinePatterns": [],
  "removeProductAliases": [],
  "learningNotes": "Tally Prime style invoice; product name comes from Art No + Brand + Size."
}
```

## Notes

- Posted supplier invoice proofs remain protected.
- Existing reset/delete actions remain available for full cleanup.
- Manual ignore text is normalized before saving, so similar future OCR rows can be skipped for the same vendor.
