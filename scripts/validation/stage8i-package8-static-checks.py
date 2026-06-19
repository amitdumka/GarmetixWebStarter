from pathlib import Path
root = Path(__file__).resolve().parents[2]

workspace = (root / 'frontend/garmetix-web/composables/useWorkspace.ts').read_text()
for token in [
    'type WorkspaceDefaults',
    'defaultStorageKey',
    'setDefault',
    'persist',
    'readSaved(user?.id)',
    'validId(saved.storeId, allowedStores)',
]:
    if token not in workspace:
        raise SystemExit(f'Missing workspace persistence token: {token}')

shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
for token in [
    'workspacePillLabel',
    'workspacePillDescription',
    'openWorkspaceSelector',
    'Working Workspace',
    'Set as default',
    'selectedWorkspaceSummary',
    'workspace.initialize(auth.user.value, shellCompanies.value, storeGroups.value, shellStores.value, {',
]:
    if token not in shell:
        raise SystemExit(f'Missing AppShell workspace selector token: {token}')

if 'workspaceOpen = true' in shell and 'openWorkspaceSelector' not in shell:
    raise SystemExit('Workspace open should use openWorkspaceSelector helper.')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.7'", 'Stage 8I Package 8 Workspace Selector', 'GARMETIX-8I-20260618-4970']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.7"', 'Stage 8I Package 8 Workspace Selector', 'GARMETIX-8I-20260618-4970']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package8-WorkspaceSelector-v4.9.7-Notes.md',
    'docs/operations/Workspace-Selector-v4.9.7.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 8 static validation passed.')
