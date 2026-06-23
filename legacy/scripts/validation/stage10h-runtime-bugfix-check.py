from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []

def read(path: str) -> str:
    return (root / path).read_text(encoding='utf-8')

def exists(path: str) -> bool:
    return (root / path).exists()

def add(name: str, ok: bool):
    checks.append((name, ok))

app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
csproj = read('backend/Garmetix.Api/Garmetix.Api.csproj')
program = read('backend/Garmetix.Api/Program.cs')
endpoint = read('backend/Garmetix.Api/Production/RuntimeDiagnosticsEndpoints.cs')
page = read('frontend/garmetix-web/pages/runtime-diagnostics/index.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
shell = read('frontend/garmetix-web/components/AppShell.vue')
legacy = read('frontend/garmetix-web/components/AppShellLegacy.vue')
drill = read('scripts/linux/stage10h-runtime-diagnostics-drill.sh') if exists('scripts/linux/stage10h-runtime-diagnostics-drill.sh') else ''

add('runtime diagnostics release history retained', '/runtime-diagnostics' in app_info and '/runtime-diagnostics' in app_version)
add('runtime diagnostics endpoint mapped', 'app.MapRuntimeDiagnosticsEndpoints();' in program and 'MapRuntimeDiagnosticsEndpoints' in endpoint)
add('runtime diagnostics APIs', all(token in endpoint for token in ['/api/runtime-diagnostics', '/summary', '/page-contracts', '/known-runtime-checks', 'ProbeAsync', 'CountProbeAsync']))
add('database probes included', all(token in endpoint for token in ['db.Companies', 'db.Stores', 'db.Products', 'db.SalesInvoices', 'db.Employees', 'db.AttendancePunches', 'db.SalaryPaySlips', 'db.AuditLogEntries']))
add('frontend runtime page', all(token in page for token in ['Runtime Diagnostics', 'runtime-diagnostics/page-contracts', 'runtime-diagnostics/known-runtime-checks', 'Promise.allSettled']))
add('route access and menus', all(route in access and route in shell and route in legacy for route in ['/runtime-diagnostics']))
add('linux drill', all(token in drill for token in ['runtime-diagnostics', 'page-contracts', 'known-runtime-checks']))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + f': {name}')
if failed:
    raise SystemExit('Stage 10H runtime bugfix validation failed: ' + ', '.join(failed))
print('Stage 10H runtime diagnostics regression validation passed.')
