#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': [
        '4.12.08',
        'Stage 11D-93 Vyapar Import Batch Undo Barcode Mapping',
        'GARMETIX-11D93-20260629-4208',
    ],
    'frontend/garmetix-web/utils/appVersion.ts': [
        "APP_VERSION = '4.12.08'",
        'Stage 11D-93 Vyapar Import Batch Undo Barcode Mapping',
    ],
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportDtos.cs': [
        'VyaparSaleImportBatchDto',
        'VyaparSaleImportUndoRequest',
        'VyaparBarcodeMappingUploadResponse',
        'FinalApprovalConfirmed',
        'ImportBatchId',
    ],
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs': [
        'ListBatchesAsync',
        'UndoBatchAsync',
        'PreviewBarcodeMappingUploadAsync',
        'VyaparImportBatchId=',
        'AddVyaparImportAudit',
        'CancelImportedInvoiceAsync',
    ],
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportEndpoints.cs': [
        '/barcode-mapping/preview',
        '/batches',
        '/batches/{batchId:guid}/undo',
    ],
    'frontend/garmetix-web/pages/billing/vyapar-import.vue': [
        'showFinalApproval',
        'runFinalApprovedImport',
        'previewMappingUpload',
        'applyUploadedMappings',
        'finalApprovalConfirmed: true',
    ],
    'frontend/garmetix-web/pages/billing/vyapar-import-batches.vue': [
        'Vyapar Sale Import Batches',
        'runUndo',
        'Reverse Batch',
    ],
    'frontend/garmetix-web/components/AppShell.vue': [
        '/billing/vyapar-import-batches',
        'Vyapar Import Batches',
    ],
    'frontend/garmetix-web/components/AppShellLegacy.vue': [
        '/billing/vyapar-import-batches',
        'Vyapar Import Batches',
    ],
    'docs/stages/stage-11/Stage11D93-Vyapar-Import-Batch-Undo-Barcode-Mapping-v4.12.08.md': [
        'Stage 11D-93',
        'Batch undo behavior',
    ],
}

failed = False
for rel, tokens in checks.items():
    path = ROOT / rel
    if not path.exists():
        print(f'MISSING FILE: {rel}')
        failed = True
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {rel}: {token}')
            failed = True

if failed:
    print('Stage 11D-93 validation FAILED')
    sys.exit(1)
print('Stage 11D-93 validation passed')
