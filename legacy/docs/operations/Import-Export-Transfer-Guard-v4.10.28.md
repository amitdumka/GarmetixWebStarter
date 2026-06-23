# Import Export Transfer Guard - v4.10.28

## Purpose

The Import/Export page used direct browser-side API base construction for CSV downloads and import uploads. That can fail on Cloudflare Tunnel, reverse-proxy or remote-hosted deployments when the browser receives a local API base such as `localhost`.

## Implemented

- Export CSV, template CSV and import upload requests now use the hosted-safe API URL resolver.
- Export/template filenames now honor `Content-Disposition` including `filename*` when present.
- Export, template and import-error CSV filenames are sanitized for Windows, Linux and macOS.
- The validate-then-commit workflow remains unchanged: Import is still disabled when validation returns row errors.
- A dedicated acceptance guard now checks hosted-safe transfer URLs, CSV filename safety and core Import/Export engine coverage.

## Acceptance

Run:

```bash
python scripts/validation/import-export-acceptance-check.py
python scripts/validation/current-release-checks.py
```

