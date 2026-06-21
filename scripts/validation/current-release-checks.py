import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    root / 'scripts/validation/stage10k-operator-acceptance-check.py',
    root / 'scripts/validation/import-export-acceptance-check.py',
    root / 'scripts/validation/payroll-acceptance-check.py',
    root / 'scripts/validation/voucher-acceptance-check.py',
    root / 'scripts/validation/petty-cash-acceptance-check.py',
    root / 'scripts/validation/sale-invoice-acceptance-check.py',
    root / 'scripts/validation/stage10j-import-export-engine-check.py',
    root / 'scripts/validation/stage10i-store-operations-cash-closing-check.py',
    root / 'scripts/validation/stage10h-runtime-bugfix-check.py',
    root / 'scripts/validation/stage10-complete-check.py',
    root / 'scripts/validation/stage10b-import-export-center-check.py',
    root / 'scripts/validation/app-version-string-safety-check.py',
    root / 'scripts/validation/frontend-route-access-check.py',
    root / 'scripts/validation/navigation-menu-coverage-check.py',
    root / 'scripts/validation/secret-hygiene-check.py',
]

for check in checks:
    print(f"\n== {check.name} ==")
    subprocess.run([sys.executable, str(check)], cwd=root, check=True)

print("\nCurrent release validation passed for Stage 10K Production Operator Acceptance / v4.10.29.")
