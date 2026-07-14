# SRP DotMatrix Bridge

This folder contains the SRP/local-PostgreSQL DotMatrix bridge used by the
Version6 deployment on Ubuntu Desktop.

## Install

Copy both scripts to the target host and run:

```bash
sudo GARMETIX_DOTMATRIX_PRINTER=EPSON_LX310 ./install-dotmatrix-localpg-srp.sh
```

The installer:

- installs CUPS, CUPS client tools and PostgreSQL client tools;
- creates a raw CUPS queue for an attached Epson LX-310 USB printer;
- writes `/etc/garmetix-dotmatrix.env` from `/etc/garmetix/srp-api.env`;
- installs `garmetix-dotmatrix-bridge.service`;
- starts the bridge service.

The bridge uses the same database queue tables as the app and applies the
LX-310 CRLF/raw-print hotfix before sending jobs to CUPS.

## Checks

```bash
systemctl status garmetix-dotmatrix-bridge.service --no-pager
lpstat -p EPSON_LX310 -l
journalctl -u garmetix-dotmatrix-bridge.service -n 80 --no-pager
```
