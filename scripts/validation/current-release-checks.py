import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    root / "scripts/validation/stage8a-package1-static-checks.py",
    root / "scripts/validation/stage8a-package2-static-checks.py",
    root / "scripts/validation/stage8a-package3-static-checks.py",
    root / "scripts/validation/stage8a-package4-static-checks.py",
    root / "scripts/validation/stage8a-package5-static-checks.py",
    root / "scripts/validation/stage8a-package6-static-checks.py",
    root / "scripts/validation/stage8a-package7-static-checks.py",
    root / "scripts/validation/stage8a-package8-static-checks.py",
    root / "scripts/validation/stage8a-package9-static-checks.py",
]

for check in checks:
    print(f"\n== {check.name} ==")
    subprocess.run([sys.executable, str(check)], cwd=root, check=True)

print("\nCurrent release validation passed.")
