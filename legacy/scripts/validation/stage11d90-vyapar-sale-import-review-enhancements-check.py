#!/usr/bin/env python3
from pathlib import Path
import sys
checks = {
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportDtos.cs': [
        'FullyMatchedInvoiceCount', 'AutoHiddenExistingInvoiceCount', 'VyaparSaleImportPaymentSourceDto', 'VyaparPaymentBankMappingDto'
    ],
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs': [
        'ListImportedAsync', 'BuildPaymentBankMap', 'ExtractCustomerNameFromParty', 'VyaparSourceInvoice=', 'BuildImportInvoiceRemark', 'PaymentDetailsJson = payment.PaymentDetailsJson'
    ],
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportEndpoints.cs': ['/imported'],
    'frontend/garmetix-web/pages/billing/vyapar-import.vue': ['invoiceFilter', 'fullyMatchedInvoices', 'paymentBankMappings', 'Import Fully Matched'],
    'frontend/garmetix-web/pages/billing/vyapar-imported.vue': ['Imported Vyapar Sale Invoices', 'sourceInvoiceNumber'],
    'backend/Garmetix.Domain/Generated/Models/Inventory/Invoicing.cs': ['public string? Remarks { get; set; }'],
    'backend/Garmetix.Infrastructure/Data/Migrations/20260628193000_AddSaleInvoiceRemarks.cs': ['AddColumn<string>', 'SalesInvoices', 'Remarks'],
    'backend/Garmetix.Api/Billing/BillingDtos.cs': ['string? Remarks = null'],
    'backend/Garmetix.Api/Billing/BillingEndpoints.cs': ['request.Remarks', 'invoice.Remarks'],
    'frontend/garmetix-web/pages/billing/new.vue': ['form.remarks', 'Invoice remark / note'],
    'frontend/garmetix-web/pages/billing/index.vue': ['editInvoiceForm.remarks', "{ accessorKey: 'remarks'"],
    'frontend/garmetix-web/components/AppShell.vue': ['/billing/vyapar-imported'],
    'frontend/garmetix-web/components/AppShellLegacy.vue': ['/billing/vyapar-imported'],
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.05', 'Stage 11D-90 Vyapar Sale Import Review Enhancements'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.05'", 'Stage 11D-90 Vyapar Sale Import Review Enhancements'],
}
missing=[]
for file,tokens in checks.items():
    path=Path(file)
    if not path.exists():
        missing.append(f'MISSING FILE: {file}')
        continue
    text=path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            missing.append(f'MISSING TOKEN in {file}: {token}')
if missing:
    print('\n'.join(missing))
    print('Stage 11D-90 validation FAILED')
    sys.exit(1)
print('Stage 11D-90 validation passed')
