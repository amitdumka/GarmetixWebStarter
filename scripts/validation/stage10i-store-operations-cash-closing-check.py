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
store_day_api = read('backend/Garmetix.Api/StoreDay/StoreDayEndpoints.cs')
store_day_page = read('frontend/garmetix-web/pages/store-day/index.vue')
drill = read('scripts/linux/stage10i-store-operations-cash-closing-drill.sh') if exists('scripts/linux/stage10i-store-operations-cash-closing-drill.sh') else ''

add('version identity', (('Stage 10I Store Operations Cash Closing Repair' in app_info and 'GARMETIX-10I-20260620-4116' in app_info and "APP_VERSION = '4.10.16'" in app_version and '<Version>4.10.16</Version>' in csproj) or ('Stage 10J Real Excel Import Export Engine' in app_info and 'GARMETIX-10J-20260620-4117' in app_info and "APP_VERSION = '4.10.17'" in app_version and '<Version>4.10.17</Version>' in csproj) or ('Stage 10J Real Excel Import Export Engine' in app_info and 'GARMETIX-10J-20260620-4118' in app_info and "APP_VERSION = '4.10.18'" in app_version and '<Version>4.10.18</Version>' in csproj) or ('Stage 10J Real Excel Import Export Engine' in app_info and 'GARMETIX-10J-20260620-4119' in app_info and "APP_VERSION = '4.10.19'" in app_version and '<Version>4.10.19</Version>' in csproj) or ('Stage 10J Real Excel Import Export Engine' in app_info and 'GARMETIX-10J-20260620-4120' in app_info and "APP_VERSION = '4.10.20'" in app_version and '<Version>4.10.20</Version>' in csproj) or ('Stage 10J Real Excel Import Export Engine' in app_info and 'GARMETIX-10J-20260620-4121' in app_info and "APP_VERSION = '4.10.21'" in app_version and '<Version>4.10.21</Version>' in csproj) or ('Stage 10J Real Excel Import Export Engine' in app_info and 'GARMETIX-10J-20260620-4122' in app_info and "APP_VERSION = '4.10.22'" in app_version and '<Version>4.10.22</Version>' in csproj) or ('Stage 10J Real Excel Import Export Engine' in app_info and 'GARMETIX-10J-20260620-4123' in app_info and "APP_VERSION = '4.10.23'" in app_version and '<Version>4.10.23</Version>' in csproj) or ('Stage 10J Real Excel Import Export Engine' in app_info and 'GARMETIX-10J-20260620-4124' in app_info and "APP_VERSION = '4.10.24'" in app_version and '<Version>4.10.24</Version>' in csproj)))
if not checks[0][1]:
    checks[0] = ('version identity', (
        'Stage 10J Real Excel Import Export Engine' in app_info
        and (
            ('GARMETIX-10J-20260620-4125' in app_info and "APP_VERSION = '4.10.25'" in app_version and '<Version>4.10.25</Version>' in csproj)
            or ('GARMETIX-10J-20260620-4126' in app_info and "APP_VERSION = '4.10.26'" in app_version and '<Version>4.10.26</Version>' in csproj)
            or ('GARMETIX-10J-20260620-4127' in app_info and "APP_VERSION = '4.10.27'" in app_version and '<Version>4.10.27</Version>' in csproj)
            or ('GARMETIX-10J-20260620-4128' in app_info and "APP_VERSION = '4.10.28'" in app_version and '<Version>4.10.28</Version>' in csproj)
        )
    ) or (
        'Stage 10K Production Operator Acceptance' in app_info
        and 'GARMETIX-10K-20260620-4129' in app_info
        and "APP_VERSION = '4.10.29'" in app_version
        and '<Version>4.10.29</Version>' in csproj
    ) or (
        'Stage 10L Production Support Pack' in app_info
        and 'GARMETIX-10L-20260620-4130' in app_info
        and "APP_VERSION = '4.10.30'" in app_version
        and '<Version>4.10.30</Version>' in csproj
    ) or (
        'Stage 10M Production Rehearsal Tracker' in app_info
        and 'GARMETIX-10M-20260620-4131' in app_info
        and "APP_VERSION = '4.10.31'" in app_version
        and '<Version>4.10.31</Version>' in csproj
    ) or (
        'Stage 11A MAUI Android Attendance Kiosk Shell' in app_info
        and 'GARMETIX-11A-20260621-4110' in app_info
        and "APP_VERSION = '4.11.0'" in app_version
        and '<Version>4.11.0</Version>' in csproj
    ) or (
        'Stage 11A Android Build Hardening' in app_info
        and 'GARMETIX-11A-20260621-4111' in app_info
        and "APP_VERSION = '4.11.1'" in app_version
        and '<Version>4.11.1</Version>' in csproj
    ) or (
        'Stage 11A Physical Tablet Rehearsal' in app_info
        and 'GARMETIX-11A-20260621-4112' in app_info
        and "APP_VERSION = '4.11.2'" in app_version
        and '<Version>4.11.2</Version>' in csproj
    ))
add('day closing uses day open opening', all(token in store_day_api for token in ['DayBegins.AsNoTracking()', 'OpeningBalance = opening', 'Today day opening', "Opening balance is taken from today's Day Open entry"]))
add('previous petty cash mismatch control', all(token in store_day_api for token in ['GetPreviousPettyCashClosingInfoAsync', 'OpeningBalanceMismatch', 'ConfirmOpeningBalanceMismatch', 'Results.Conflict', 'PreviousPettyCashClosingBalance']))
add('editable petty cash preview request', all(token in store_day_api for token in ['PettyCashSheetDraftDto', 'ApplyPettyCashDraft', 'PettyCashSheet = null', 'Petty cash sheet values were reviewed/edited']))
add('store operations preview popup', all(token in store_day_page for token in ['closingPreviewOpen', 'Day Closing Petty Cash Preview', 'Preview Petty Cash + Close', 'pettyCashPreview', 'closeWithMismatchConfirmation']))
add('host drill', all(token in drill for token in ['store-day/status', 'store-day/book-summary', 'stage10i store operations']))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + f': {name}')
if failed:
    raise SystemExit('Stage 10I store operations cash closing validation failed: ' + ', '.join(failed))
print('Stage 10I store operations cash closing validation passed.')
