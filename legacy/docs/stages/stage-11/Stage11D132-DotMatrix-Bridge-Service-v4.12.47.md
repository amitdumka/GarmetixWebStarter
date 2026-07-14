# Stage 11D-132 — DotMatrix Bridge Service (v4.12.47)

## Goal

Refactor dot-matrix printing into a production-safe architecture where the Garmetix API only creates database queue rows and the Ubuntu host-side bridge service does the actual Epson LX-810 printing.

## Implemented

- Default printer queue set to `EPSON_LX810`.
- API print worker disabled by default.
- New `BridgeService` output mode.
- Ubuntu host-side `garmetix-dotmatrix-bridge.sh` service.
- Systemd service: `garmetix-dotmatrix-bridge.service`.
- Installer: `deploy/install-dotmatrix-bridge-ubuntu.sh`.
- Old spool-file installer now wraps the bridge installer.
- Production env/compose defaults updated.
- Dot Matrix UI updated to show bridge mode as the recommended option.
- `README-dotmatrix.md` added with setup, usage, operations and troubleshooting.

## Safety behavior

- Main API does not call `lp`.
- Main API does not depend on USB printer availability.
- Bridge marks queue row `Printed` only after `lp` returns success.
- Failed printer jobs become `Failed` and can be retried.
- Stale `Printing` rows are recovered after bridge restart.

## Recommended production settings

```env
DOTMATRIX_PRINTING_ENABLED=false
DOTMATRIX_PRINTING_RUN_WORKER=false
DOTMATRIX_PRINTER_NAME=EPSON_LX810
DOTMATRIX_OUTPUT_MODE=BridgeService
DOTMATRIX_TIMEZONE_ID=Asia/Kolkata
DOTMATRIX_LINE_WIDTH=136
```

Enable printing store-wise from the Dot Matrix Print admin page after CUPS printer verification.
