#!/usr/bin/env python3
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
subprocess.run([sys.executable, str(ROOT / 'scripts/validation/backup-restore-safety-check.py')], cwd=ROOT, check=True)
print('Stage 8I Package 17 static checks passed.')
