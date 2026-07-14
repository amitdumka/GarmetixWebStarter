from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

closure = root / 'backend/Garmetix.Api/Inventory/StockValuationClosureEndpoints.cs'
text = closure.read_text()
add('closure summary dto renamed', 'StockValuationClosureSummaryDto' in text)
add('closure row dto renamed', 'StockValuationClosureRowDto' in text)
add('old summary record not declared in closure', 'public sealed record StockValuationSummaryDto' not in text)
add('old row record not declared in closure', 'public sealed record StockValuationRowDto' not in text)
add('version updated', '4.12.67' in (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text())
add('build code updated', 'GARMETIX-11D152-20260703-4267' in (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text())
add('release note exists', (root / 'RELEASE-v4.12.67.txt').exists())

# Detect duplicate public record/class names inside the same file-scoped namespace.
pat_ns = re.compile(r'^namespace\s+([\w\.]+)\s*;', re.M)
pat_type = re.compile(r'public\s+(?:sealed\s+)?(?:partial\s+)?(?:record|class|struct)\s+([A-Za-z_][\w]*)')
seen = {}
duplicates = []
for path in (root / 'backend/Garmetix.Api').rglob('*.cs'):
    content = path.read_text(errors='ignore')
    ns_match = pat_ns.search(content)
    ns = ns_match.group(1) if ns_match else ''
    for match in pat_type.finditer(content):
        key = (ns, match.group(1))
        if key in seen:
            duplicates.append((key, seen[key], str(path)))
        else:
            seen[key] = str(path)
add('no duplicate public type names in API namespaces', not duplicates)

for name, ok in checks:
    print(f'{"PASS" if ok else "FAIL"}: {name}')
if not all(ok for _, ok in checks):
    if duplicates:
        print('Duplicates:')
        for key, first, second in duplicates:
            print(key, first, second)
    sys.exit(1)
print(f'All {len(checks)} Stage 11D-152 validation checks passed.')
