#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []

def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding='utf-8')

def exists(rel: str) -> bool:
    return (ROOT / rel).exists()

def add(name: str, ok: bool):
    if not ok:
        errors.append(name)

endpoint = read('backend/Garmetix.Api/Production/PrintAcceptanceEndpoints.cs')
page = read('frontend/garmetix-web/pages/print-final-acceptance/index.vue')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
linux_drill = read('scripts/linux/print-pdf-acceptance-drill.sh') if exists('scripts/linux/print-pdf-acceptance-drill.sh') else ''

required_keys = [
    'salesInvoice',
    'salesReturn',
    'voucher',
    'cashVoucher',
    'pettyCash',
    'purchaseInward',
    'purchaseReturn',
    'commercialNote',
    'nonGstGoods',
    'tailoringOrder',
    'salaryPayslip',
    'salaryPayment',
    'gstReturn',
]

for key in required_keys:
    add(f'print acceptance key {key}', f'"{key}"' in endpoint and key in linux_drill)

for endpoint_path in [
    '/api/billing/sales/',
    '/api/purchase/returns/',
    '/api/commercial-notes/',
    '/api/non-gst-goods/documents/',
    '/api/payroll/payslips/',
    '/api/payroll/',
    '/api/gst-returns/drafts/',
]:
    add(f'print endpoint path {endpoint_path}', endpoint_path in endpoint)

add('print UI expanded description', 'sales invoices, returns, vouchers, petty cash, purchase, payroll, tailoring, non-GST goods and GST exports' in page)
add('print UI PDF checklist', 'PDF output' in page and 'expected paper size' in page)
add('print UI quick navigation expanded', 'to="/billing"' in page and 'to="/sales-return"' in page and 'to="/purchase-return"' in page and 'to="/non-gst-goods"' in page and 'to="/payroll"' in page)
add('print manifest code', 'PRINT_PDF_ACCEPTANCE' in catalog and 'print-pdf-acceptance-check.py' in catalog and 'print-pdf-acceptance-drill.sh' in catalog)
add('print drill auth and catalog', 'GARMETIX_SMOKE_USER' in linux_drill and 'print-acceptance/status' in linux_drill and 'MAX_SAMPLE_FETCHES' in linux_drill)

if errors:
    print('Print/PDF acceptance static check failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('Print/PDF acceptance static check passed.')
