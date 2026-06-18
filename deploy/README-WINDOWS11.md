# Deploying Garmetix from Windows 11

The default `deploy/deploy-to-macmini.sh` script is a Bash script. It works directly from Linux, macOS, or Windows WSL Ubuntu. It does **not** run directly in normal Windows Command Prompt or PowerShell.

## Best option: use WSL Ubuntu on Windows 11

Open PowerShell as Administrator once and install WSL if you do not already have it:

```powershell
wsl --install -d Ubuntu
```

Then open **Ubuntu** from the Start menu and run:

```bash
sudo apt update
sudo apt install -y openssh-client sshpass tar curl jq
cd /mnt/c/path/to/GarmetixWebStarter
cp deploy/macmini.env.example deploy/macmini.env
nano deploy/macmini.env
chmod +x deploy/*.sh 2>/dev/null || true
./deploy/deploy-to-macmini.sh
```

This is the most compatible way because the original deploy scripts use Bash tools.

## Native Windows PowerShell option

A Windows helper script is included:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\deploy\deploy-to-macmini-windows.ps1
```

Requirements:

- Windows 11 OpenSSH Client enabled
- `ssh`, `scp`, and `tar` available in PATH
- SSH key login is recommended

### Set up SSH key login from Windows

```powershell
ssh-keygen -t ed25519 -C "garmetix-windows-deploy"
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh amit@192.168.11.126 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys"
ssh amit@192.168.11.126
```

After this, run:

```powershell
.\deploy\deploy-to-macmini-windows.ps1
```

## Important

`192.168.11.126` is only your LAN IP. Public access should use Cloudflare Tunnel for:

```text
garmetix.aadwikafashion.in
```

Do not commit or share these files after filling secrets:

- `deploy/macmini.env`
- `.env.production`
