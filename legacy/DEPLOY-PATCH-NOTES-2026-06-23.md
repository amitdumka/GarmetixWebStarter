# Garmetix Deploy Patch Notes - 2026-06-23

This package includes:

- AF/SS seeder update: no inventory/product/stock seeding.
- Smart Menswear-only employee/salesman seeding.
- Compile fix for `Tax` reference in `AfssDefaultSeederService.cs`.
- Generic Ubuntu deployment scripts.
- Saved SSH/sudo password support through `~/.garmetix/ubuntu.env`.
- Cloudflare tunnel ID/token support via `-t`, `-tun`, and `-tok`.
- Fresh DB reset option via `--reset-db`.
- Postgres role fix: scripts no longer assume a DB role named `postgres`; they connect using configured `POSTGRES_USER` first.

Main deploy command:

```bash
./deploy/deploy-to-ubuntu.sh -s amit@192.168.11.126 -t T1 --update-env --reset-db
```

If Docker is already installed and you want to skip host preparation:

```bash
./deploy/deploy-to-ubuntu.sh -s amit@192.168.11.126 -t T1 --update-env --reset-db --no-install
```
