# Stage 8G Package 9 - Final Go-Live Acceptance (v4.6.8)

This package completes Stage 8G by adding the final operational acceptance layer.

## Included

- Stage 8G Completion admin page.
- Final go-live acceptance script for Mac mini production checks.
- Role acceptance matrix script and documentation.
- Production release checklist documentation.
- Runtime smoke version check updated to follow the current Stage 8G build instead of an old hard-coded v4.6.0 value.

## Final command

```bash
cd /opt/garmetix/current
./scripts/linux/stage8g-final-acceptance-check.sh .env.production
```
