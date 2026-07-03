#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = [
    ("backend/Garmetix.Api/Payroll/PayrollFinalizationService.cs", [
        "class PayrollFinalizationService",
        "CreateExecutionStrategy",
        "BeginTransactionAsync",
        "PostSalaryPaymentAsync",
        "MarkRecoveredPayrollAdjustments",
        "OvertimeRateMultiplier",
        "LatePenaltyPerDay",
        "PayrollFinalized",
    ]),
    ("backend/Garmetix.Api/Payroll/PayrollEndpoints.cs", [
        '"/finalization/finalize-month"',
        "PayrollFinalizationService",
    ]),
    ("backend/Garmetix.Api/Payroll/PayrollDtos.cs", [
        "PayrollFinalizeMonthRequest",
        "PayrollFinalizeMonthResponse",
    ]),
    ("backend/Garmetix.Api/Program.cs", [
        "AddScoped<PayrollFinalizationService>",
    ]),
    ("frontend/garmetix-web/pages/payroll/finalization.vue", [
        "Finalize Payroll Safely",
        "payroll/finalization/finalize-month",
        "overtimeRateMultiplier",
        "latePenaltyPerDay",
        "postSalaryPayments",
        "lockAfterFinalization",
    ]),
    ("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs", [
        'Version = "4.11.55"',
        "Stage 11D-40 Payroll Finalization Hardening",
    ]),
    ("frontend/garmetix-web/utils/appVersion.ts", [
        "APP_VERSION = '4.11.55'",
        "Stage 11D-40 Payroll Finalization Hardening",
    ]),
]

missing = []
for rel, needles in checks:
    path = ROOT / rel
    if not path.exists():
        missing.append(f"Missing file: {rel}")
        continue
    text = path.read_text(errors="replace")
    for needle in needles:
        if needle not in text:
            missing.append(f"Missing '{needle}' in {rel}")

if missing:
    print("Stage 11D-40 payroll finalization hardening static check FAILED")
    for item in missing:
        print(f"- {item}")
    sys.exit(1)

print("Stage 11D-40 payroll finalization hardening static check PASSED")
