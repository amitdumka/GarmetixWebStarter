# Dot Matrix Live Transaction Polish — v4.12.48

This module update hardens the v4.12.47 DotMatrix Bridge foundation.

## Practical behavior

- Day opening prints cash details first.
- Transactions print immediately after DB save when store printing is enabled.
- Edits/deletes print correction audit lines instead of changing old paper records.
- Day closing prints cash details and a full-width summary.
- Failed printer jobs are retried by queue controls and bridge retry rules.

## Store operator controls

Open `Accounting → Dot Matrix Print`.

- Enable/disable printing per store.
- Set printer queue name, default `EPSON_LX810`.
- Use 136-column full-width mode for paper-saving summary.
- Retry all failed rows.
- Reset stale `Printing` rows if the bridge was stopped mid-print.
- Preview the raw text sent to the printer.

## Production note

Keep `DOTMATRIX_PRINTING_RUN_WORKER=false` in production. The main API should never depend on printer hardware.
