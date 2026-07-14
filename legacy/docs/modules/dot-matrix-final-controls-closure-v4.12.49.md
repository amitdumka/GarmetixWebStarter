# Dot Matrix Final Controls Closure — v4.12.49

This closes the code-level DotMatrix Daily Audit Journal feature.

## Final controls added

- Pause/resume per store.
- Retry failed queue rows.
- Reset stuck `Printing` rows.
- Reprint exact queue row with paper `REPRINT COPY` header.
- Skip row where printing is intentionally not required.
- Raw text preview before paper print.
- Queue status counters.
- Ubuntu bridge status script.

## Production rule

Keep:

```env
DOTMATRIX_PRINTING_RUN_WORKER=false
DOTMATRIX_OUTPUT_MODE=BridgeService
DOTMATRIX_PRINTER_NAME=EPSON_LX810
```

Only the Ubuntu bridge should talk to Epson/CUPS.
