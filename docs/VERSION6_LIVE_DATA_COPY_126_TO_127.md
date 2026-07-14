# Version6 Live Data Copy: 192.168.11.126 to 192.168.11.127

Purpose: copy the PostgreSQL live database from the existing legacy host `192.168.11.126` to the target host `192.168.11.127`.

The script is:

```bash
legacy/deploy/sync-postgres-live-data.sh
```

Default assumptions:

- Source host: `192.168.11.126`
- Source SSH user: `amit`
- Target host: `192.168.11.127`
- Target SSH user: `amitkumar`
- App directory on both hosts: `/opt/garmetix`
- Compose file: `docker-compose.prod.yml`
- Database values are read from each host `.env` file. If absent, it falls back to `garmetix` / `garmetix`.

Run from WSL:

```bash
cd /mnt/c/Users/amitn/Documents/Codex/2026-06-04/i-have-class-model-written-in/outputs/GarmetixWebStarter
export GMX_CONFIRM_RESTORE=YES
export GMX_SSH_PASSWORD='set-this-in-shell-only'
export GMX_SOURCE_SSH_USER=amit
export GMX_TARGET_SSH_USER=amitkumar
export GMX_SOURCE_APP_DIR=/opt/garmetix
export GMX_TARGET_APP_DIR=/opt/garmetix
bash legacy/deploy/sync-postgres-live-data.sh
```

Safety notes:

- The script refuses to run unless `GMX_CONFIRM_RESTORE=YES`.
- It does not store SSH or sudo passwords in git.
- It stops `api` and `web` on the target during restore, recreates the target database, restores the source dump, then starts the compose stack again.
- Run this only when overwriting the target server database is intended.
