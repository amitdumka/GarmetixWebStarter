import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    root / 'scripts/validation/stage11d118-public-privacy-terms-pages-check.py',
    root / 'scripts/validation/app-version-string-safety-check.py',
    root / 'scripts/validation/frontend-route-access-check.py',
    root / 'scripts/validation/navigation-menu-coverage-check.py',
    root / 'scripts/validation/secret-hygiene-check.py',
]

for check in checks:
    print(f"\n== {check.name} ==")
    subprocess.run([sys.executable, str(check)], cwd=root, check=True)

print("\nCurrent release validation passed for Stage 11D-118 Public Privacy and Terms Pages / v4.12.33.")
