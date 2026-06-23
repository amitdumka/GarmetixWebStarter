# WSL deploy hotfix: Windows path, IP 192.168.11.126

This package is patched for deploying from WSL even when the source folder is under `/mnt/c`.

Changes:

- Mac mini host changed to `192.168.11.126`.
- Replaced `sed -i` env rewrites with a portable env writer. This avoids `sed: preserving permissions ... Operation not permitted` on Windows-mounted files.
- Replaced fixed `/tmp/garmetix-cf-*.err` files with `mktemp` files. This avoids permission conflicts after switching between normal and sudo runs.
- `chmod` is now best-effort on local files so `/mnt/c` does not stop deployment.

Recommended run from WSL:

```bash
cd ~/GarmetixWebStarter
# or your existing /mnt/c path with this hotfix package
nano deploy/macmini.env
chmod +x deploy/*.sh 2>/dev/null || true
./deploy/deploy-to-macmini.sh
```

Do not run the local WSL deploy script with sudo. The remote Mac mini install script will use sudo on the remote machine when needed.

If you already created root-owned temp files from a sudo attempt, clean them once:

```bash
sudo rm -f /tmp/garmetix-cf-*.err /tmp/garmetix-cf-*
```
