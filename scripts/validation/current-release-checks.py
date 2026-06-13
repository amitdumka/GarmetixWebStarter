import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    root / "scripts/validation/stage8a-package1-static-checks.py",
    root / "scripts/validation/stage8a-package2-static-checks.py",
    root / "scripts/validation/stage8a-package3-static-checks.py",
]

for check in checks:
    print(f"\n== {check.name} ==")
    subprocess.run([sys.executable, str(check)], cwd=root, check=True)

print("\nCurrent release validation passed.")
