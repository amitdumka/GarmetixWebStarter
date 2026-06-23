from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
service = root / "backend/Garmetix.Api/Accounting/AccountingPostingService.cs"
text = service.read_text(encoding="utf-8")

errors = []

manual_match = re.search(
    r"private async Task UpsertManualChequeLogAsync\([\s\S]*?\n    }\n\n    private",
    text,
)
if not manual_match:
    errors.append("Could not locate UpsertManualChequeLogAsync block")
else:
    manual_block = manual_match.group(0)
    if "paymentReference" in manual_block:
        errors.append("UpsertManualChequeLogAsync still references paymentReference, which is not in scope")
    if "var chequeReference = transaction.Reference ?? string.Empty;" not in manual_block:
        errors.append("Manual cheque log should use the bank transaction reference directly")

invoice_match = re.search(
    r"private async Task UpsertInvoiceChequeLogAsync\([\s\S]*?\n    }\n\n    private",
    text,
)
if not invoice_match:
    errors.append("Could not locate UpsertInvoiceChequeLogAsync block")
else:
    invoice_block = invoice_match.group(0)
    if "string? paymentReference" not in invoice_block:
        errors.append("Invoice cheque log should still accept paymentReference")
    if ": paymentReference.Trim();" not in invoice_block:
        errors.append("Invoice cheque log should still preserve paymentReference when supplied")

if errors:
    print("Stage 8E Package 2 compile hotfix static validation failed:")
    for error in errors:
        print(f" - {error}")
    raise SystemExit(1)

print("Stage 8E Package 2 compile hotfix static validation passed.")
