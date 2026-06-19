import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    root / "scripts/validation/stage8i-package23b-static-checks.py",
    root / "scripts/validation/hr-employee-master-check.py",
    root / "scripts/validation/hr-benefits-payroll-check.py",
    root / "scripts/validation/frontend-route-access-check.py",
    root / "scripts/validation/secret-hygiene-check.py",
]

for check in checks:
    print(f"\n== {check.name} ==")
    subprocess.run([sys.executable, str(check)], cwd=root, check=True)

print("\nCurrent release validation passed for Stage 8I Package 23B / v4.9.24.")
