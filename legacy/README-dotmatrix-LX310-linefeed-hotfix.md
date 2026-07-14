# Garmetix DotMatrix LX-310 Line Feed Hotfix

This patch is only for the Ubuntu printer bridge. It does not require a full Garmetix redeploy or Docker rebuild.

## Problem fixed

On Epson LX-310 raw printing, if text is sent with LF-only newlines (`\n`), the printer may move to the next line without returning to the left margin. This makes the second line start from the horizontal position of the previous line.

## Fix

The bridge now:

1. Prepends `ESC @` before every job to initialize/reset the Epson printer.
2. Converts every line to CRLF (`\r\n`).
3. Guarantees the print job ends with CRLF.

## Files in this patch

```text
deploy/garmetix-dotmatrix-bridge.sh
deploy/fix-dotmatrix-lx310-linefeed-ubuntu.sh
README-dotmatrix-LX310-linefeed-hotfix.md
```

## Apply on Ubuntu hosted system

```bash
cd /tmp
unzip -o GarmetixWebStarter-v4.12.52-dotmatrix-lx310-linefeed-hotfix-files.zip -d dotmatrix-linefeed-patch
sudo rsync -av dotmatrix-linefeed-patch/ /opt/garmetix/current/
cd /opt/garmetix/current
sudo chmod +x deploy/*.sh
sudo ./deploy/fix-dotmatrix-lx310-linefeed-ubuntu.sh
```

## Direct test

```bash
printf 'LEFT-1\nLEFT-2\nLEFT-3\n' | awk '{ printf "%s\r\n", $0 }' | lp -d EPSON_LX310 -o raw
```

All three lines should start from the left margin.

## App test

Open `Accounting → Dot Matrix Print`, queue a test print, then create one attendance punch or one small transaction.

