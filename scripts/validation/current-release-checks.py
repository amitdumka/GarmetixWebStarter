import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    root / "scripts/validation/stage8i-package19-static-checks.py",
]

for check in checks:
    print(f"\n== {check.name} ==")
    subprocess.run([sys.executable, str(check)], cwd=root, check=True)

print("\nCurrent release validation passed for Stage 8I Package 19 / v4.9.18.")
