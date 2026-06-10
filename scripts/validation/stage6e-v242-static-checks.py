#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []

def read(rel):
    return (root / rel).read_text(encoding='utf-8')

app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
msg_endpoints = read('backend/Garmetix.Api/Messages/ApplicationMessageLogEndpoints.cs')
msg_service = read('backend/Garmetix.Api/Messages/ApplicationMessageLogService.cs')
non_gst_api = read('backend/Garmetix.Api/NonGstGoods/NonGstGoodsEndpoints.cs')
non_gst_page = read('frontend/garmetix-web/pages/non-gst-goods/index.vue')
frontend_version = read('frontend/garmetix-web/utils/appVersion.ts')

checks.append(('AppInfo duplicate trailing-slash route removed', 'group.MapGet("/", Info)' not in app_info))
checks.append(('MessageLogs duplicate trailing-slash GET removed', 'group.MapGet("/", SearchAsync)' not in msg_endpoints))
checks.append(('MessageLogs duplicate trailing-slash POST removed', 'group.MapPost("/", CreateAsync)' not in msg_endpoints))
checks.append(('MessageLogs uses dynamic typed-safe SQL', 'BuildSearchSql' in msg_service and '(@level IS NULL' not in msg_service and '1 = 1' in msg_service))
checks.append(('Non-GST sale sends null for blank stockId', 'stockId: line.stockId || null' in non_gst_page))
checks.append(('Non-GST purchase auto-generates barcode', 'GenerateNonGstBarcode' in non_gst_api and 'Barcode is required for new Non-GST purchase stock' not in non_gst_api))
checks.append(('Non-GST invalid operations return BadRequest', 'catch (InvalidOperationException ex)' in non_gst_api and 'Results.BadRequest(new { message = ex.Message })' in non_gst_api))
checks.append(('Backend version is 2.4.2', 'public const string Version = "2.4.2"' in app_info and 'GARMETIX-6E-20260610-242' in app_info))
checks.append(('Frontend version is 2.4.2', "APP_VERSION = '2.4.2'" in frontend_version and 'GARMETIX-6E-20260610-242' in frontend_version))

for rel in [
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
    'backend/Garmetix.Api/Messages/ApplicationMessageLogEndpoints.cs',
    'backend/Garmetix.Api/Messages/ApplicationMessageLogService.cs',
    'backend/Garmetix.Api/NonGstGoods/NonGstGoodsEndpoints.cs',
]:
    text = read(rel)
    checks.append((f'Brace balance: {rel}', text.count('{') == text.count('}')))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'} - {name}")

if failed:
    raise SystemExit('Failed checks: ' + ', '.join(failed))
