# Codex Workspace Instructions

These local project instructions are for future Garmetix Version6 work.

## Active Codex Workspace

- Standard Codex workspace: `C:\AIArea\Codex\GarmetixWebStarter`
- Active branch for Codex-owned work: `version6`
- Deploy modular SRP site from this C-drive SSD Codex workspace.
- Keep deployment config and secrets outside git under the local user config folder.

## Antigravity Workspace Boundary

- Antigravity workspace: `D:\AIArea\GarmetixWebStarter`
- Antigravity branch: `Version6.A`
- Codex must not read, edit, merge, reset, deploy, or clean the Antigravity workspace unless the user explicitly asks for a specific file or comparison from that location.

## Legacy C Workspace

- Old Codex/output workspace: `C:\Users\amitn\Documents\Codex\2026-06-04\i-have-class-model-written-in\outputs\GarmetixWebStarter`
- Treat the old C workspace as backup/reference only.
- Do not use it as the normal deploy path after the C-drive `C:\AIArea\Codex\GarmetixWebStarter` workspace is verified.

## Previous D Workspace

- Previous Codex workspace: `D:\AIArea\Codex\GarmetixWebStarter`
- Treat it as backup/reference only after the C-drive SSD workspace is verified.

## Deploy Target

- SRP host: `amitkumar@192.168.11.127`
- SRP public URL: `https://srp.aadwikafashion.in`
- SRP remote base: `/opt/garmetix-srp`
- Validate both LAN and public routes after deploy.
