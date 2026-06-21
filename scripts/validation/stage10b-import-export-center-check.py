from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []

def read(path: str) -> str:
    return (root / path).read_text(encoding='utf-8')

def add(name: str, ok: bool):
    checks.append((name, ok))

app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
csproj = read('backend/Garmetix.Api/Garmetix.Api.csproj')
import_export = read('backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs')
import_page = read('frontend/garmetix-web/pages/import-export/index.vue')
final_endpoint = read('backend/Garmetix.Api/Production/Stage10AFinalAcceptanceEndpoints.cs')
final_page = read('frontend/garmetix-web/pages/production-final-acceptance/index.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
shell = read('frontend/garmetix-web/components/AppShell.vue')
manifest = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
linux_drill = read('scripts/linux/stage10b-import-export-center-drill.sh')

version_41012 = all(token in app_info for token in ['Version = "4.10.12"', 'Stage 10B Excel Import Export Center', 'GARMETIX-10B-20260620-4112']) and "APP_VERSION = '4.10.12'" in app_version and '<Version>4.10.12</Version>' in csproj
version_41013 = (all(token in app_info for token in ['Version = "4.10.13"', 'Stage 10 Complete Final Acceptance', 'GARMETIX-10F-20260620-4113']) and "APP_VERSION = '4.10.13'" in app_version and '<Version>4.10.13</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.14"', 'Stage 10G Navigation Menu Coverage', 'GARMETIX-10G-20260620-4114']) and "APP_VERSION = '4.10.14'" in app_version and '<Version>4.10.14</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.15"', 'Stage 10H Runtime Bug Fix Pack', 'GARMETIX-10H-20260620-4115']) and "APP_VERSION = '4.10.15'" in app_version and '<Version>4.10.15</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.16"', 'Stage 10I Store Operations Cash Closing Repair', 'GARMETIX-10I-20260620-4116']) and "APP_VERSION = '4.10.16'" in app_version and '<Version>4.10.16</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.17"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4117']) and "APP_VERSION = '4.10.17'" in app_version and '<Version>4.10.17</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.18"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4118']) and "APP_VERSION = '4.10.18'" in app_version and '<Version>4.10.18</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.19"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4119']) and "APP_VERSION = '4.10.19'" in app_version and '<Version>4.10.19</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.20"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4120']) and "APP_VERSION = '4.10.20'" in app_version and '<Version>4.10.20</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.21"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4121']) and "APP_VERSION = '4.10.21'" in app_version and '<Version>4.10.21</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.22"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4122']) and "APP_VERSION = '4.10.22'" in app_version and '<Version>4.10.22</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.23"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4123']) and "APP_VERSION = '4.10.23'" in app_version and '<Version>4.10.23</Version>' in csproj) or (all(token in app_info for token in ['Version = "4.10.24"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4124']) and "APP_VERSION = '4.10.24'" in app_version and '<Version>4.10.24</Version>' in csproj)
version_41013 = version_41013 or (all(token in app_info for token in ['Version = "4.10.25"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4125']) and "APP_VERSION = '4.10.25'" in app_version and '<Version>4.10.25</Version>' in csproj)
version_41013 = version_41013 or (all(token in app_info for token in ['Version = "4.10.26"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4126']) and "APP_VERSION = '4.10.26'" in app_version and '<Version>4.10.26</Version>' in csproj)
version_41013 = version_41013 or (all(token in app_info for token in ['Version = "4.10.27"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4127']) and "APP_VERSION = '4.10.27'" in app_version and '<Version>4.10.27</Version>' in csproj)
version_41013 = version_41013 or (all(token in app_info for token in ['Version = "4.10.28"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4128']) and "APP_VERSION = '4.10.28'" in app_version and '<Version>4.10.28</Version>' in csproj)
version_41013 = version_41013 or (all(token in app_info for token in ['Version = "4.10.29"', 'Stage 10K Production Operator Acceptance', 'GARMETIX-10K-20260620-4129']) and "APP_VERSION = '4.10.29'" in app_version and '<Version>4.10.29</Version>' in csproj)
version_41013 = version_41013 or (all(token in app_info for token in ['Version = "4.10.30"', 'Stage 10L Production Support Pack', 'GARMETIX-10L-20260620-4130']) and "APP_VERSION = '4.10.30'" in app_version and '<Version>4.10.30</Version>' in csproj)
version_41013 = version_41013 or (all(token in app_info for token in ['Version = "4.10.31"', 'Stage 10M Production Rehearsal Tracker', 'GARMETIX-10M-20260620-4131']) and "APP_VERSION = '4.10.31'" in app_version and '<Version>4.10.31</Version>' in csproj)
add('version identity', version_41012 or version_41013)
add('stage10a endpoint fails safe', all(token in final_endpoint for token in ['try', 'catch (Exception ex)', 'Degraded - review logs', 'CreatePayload']))
add('production final acceptance page uses allSettled', 'Promise.allSettled' in final_page and 'Some final-acceptance checks failed to load' in final_page)
add('import export center endpoints', all(token in import_export for token in ['MapGet("/center"', 'MapGet("/health"', 'ImportExportCenterAsync', 'ImportExportHealthAsync']))
add('attendance import/export support', all(token in import_export for token in ['["attendance"]', 'AttendancePunches', 'ImportAttendanceAsync', 'Duplicate attendance punch found within 5 minutes']))
add('frontend import export center updated', all(token in import_page for token in ['Excel Import / Export Center', 'import-export/center', 'attendancePunches', 'Safe Excel workflow']))
add('import export route protected', "path: '/import-export'" in access and "to: '/import-export'" in shell)
add('stage10b manifest and drill', 'STAGE10B_IMPORT_EXPORT_CENTER' in manifest and 'import-export/center' in linux_drill and 'template/attendance' in linux_drill)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + f': {name}')
if failed:
    raise SystemExit('Stage 10B Import Export Center validation failed: ' + ', '.join(failed))
print('Stage 10B Import Export Center validation passed.')
