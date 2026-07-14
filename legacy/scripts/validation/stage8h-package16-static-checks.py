from pathlib import Path
root = Path(__file__).resolve().parents[2]

deploy = (root / 'deploy/deploy-to-macmini.sh').read_text()
for token in [
    'LINK_ENV_SCRIPT=',
    'garmetix-link-env.sh',
    'bash "$LINK_ENV_SCRIPT" "$ROOT_DIR"',
    'GARMETIX_SKIP_LINK_ENV',
    'Linked persistent production env',
    '${REMOTE_APP_DIR}/shared/env/.env.production',
    'ln -sfn "${REMOTE_APP_DIR}/shared/env/.env.production" .env.production',
]:
    if token not in deploy:
        raise SystemExit(f'Missing deploy token: {token}')

# Ensure helper runs before config file is sourced/read.
helper_pos = deploy.find('bash "$LINK_ENV_SCRIPT" "$ROOT_DIR"')
config_source_pos = deploy.find('source "$CONFIG_FILE"')
archive_pos = deploy.find('Creating deployment archive')
if not (0 <= helper_pos < config_source_pos < archive_pos):
    raise SystemExit('Env link helper is not executed before config source/archive deployment.')

helper = root / 'deploy/create-garmetix-link-env-helper.sh'
if not helper.exists():
    raise SystemExit('Missing helper creator script')
helper_text = helper.read_text()
for token in ['garmetix-link-env.sh', '~/.garmetix/macmini.env', 'shared/env']:
    if token not in helper_text:
        raise SystemExit(f'Missing helper token: {token}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.8.5'", 'Stage 8H Package 16 Persistent Env Deploy Hook', 'GARMETIX-8H-20260618-4850']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8H-Package16-PersistentEnvDeploy-v4.8.5-Notes.md',
    'docs/operations/Persistent-Env-Deploy-v4.8.5.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8H Package 16 static validation passed.')
