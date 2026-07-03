# Dot Matrix Bridge Service Module — v4.12.47

This module provides the production bridge for the Dot Matrix Daily Audit Journal.

See root `README-dotmatrix.md` for operator steps.

Key files:

```text
deploy/garmetix-dotmatrix-bridge.sh
deploy/install-dotmatrix-bridge-ubuntu.sh
deploy/systemd/garmetix-dotmatrix-bridge.service
README-dotmatrix.md
```

Default printer: `EPSON_LX810`.

The bridge reads `DotMatrixPrintQueueEntries`, prints raw text through CUPS/lp, and updates queue status.
