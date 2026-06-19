import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    root / "scripts/validation/stage9e-attendance-salary-draft-check.py",
    root / "scripts/validation/frontend-route-access-check.py",
    root / "scripts/validation/secret-hygiene-check.py",
]

for check in checks:
    print(f"\n== {check.name} ==")
    subprocess.run([sys.executable, str(check)], cwd=root, check=True)

print("\nCurrent release validation passed for Stage 9E Attendance Salary Slip Draft Preview / v4.10.5.")
