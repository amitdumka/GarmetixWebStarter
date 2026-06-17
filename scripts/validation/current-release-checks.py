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
    root / "scripts/validation/stage8a-package10-static-checks.py",
    root / "scripts/validation/stage8e-package2-clean-initial-migration-static-checks.py",
    root / "scripts/validation/stage8e-package3-static-checks.py",
    root / "scripts/validation/stage8e-package4-static-checks.py",
    root / "scripts/validation/stage8e-package5-static-checks.py",
    root / "scripts/validation/stage8e-package6-static-checks.py",
    root / "scripts/validation/stage8e-package7-static-checks.py",
    root / "scripts/validation/stage8e-package7-compile-hotfix-static-checks.py",
    root / "scripts/validation/stage8f-package1-static-checks.py",
    root / "scripts/validation/stage8f-package2-static-checks.py",
    root / "scripts/validation/stage8g-package1-static-checks.py",
    root / "scripts/validation/stage8g-package1-compile-hotfix-static-checks.py",
    root / "scripts/validation/stage8g-package2-static-checks.py",
    root / "scripts/validation/stage8g-package3-static-checks.py",
    root / "scripts/validation/stage8g-package4-static-checks.py",
    root / "scripts/validation/stage8g-package5-static-checks.py",
    root / "scripts/validation/stage8g-package6-static-checks.py",
    root / "scripts/validation/stage8g-package7-static-checks.py",
    root / "scripts/validation/stage8g-package8-static-checks.py",
    root / "scripts/validation/stage8g-package9-static-checks.py",
    root / "scripts/validation/stage8g-package9-npm-registry-hotfix-static-checks.py",
    root / "scripts/validation/stage8h-package1-static-checks.py",
    root / "scripts/validation/stage8h-package2-static-checks.py",
    root / "scripts/validation/stage8h-package3-static-checks.py",
    root / "scripts/validation/stage8h-package3-compile-hotfix-static-checks.py",
]

for check in checks:
    print(f"\n== {check.name} ==")
    subprocess.run([sys.executable, str(check)], cwd=root, check=True)

print("\nCurrent release validation passed.")
