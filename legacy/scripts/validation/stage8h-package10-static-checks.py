from pathlib import Path
root = Path(__file__).resolve().parents[2]
checks = []

def must(path, *tokens):
    text = (root / path).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing token in {path}: {token}')

must('backend/Garmetix.Api/Auth/IEmailSender.cs',
     'public sealed record EmailAttachment',
     'IReadOnlyList<EmailAttachment>? Attachments')
must('backend/Garmetix.Api/Auth/SmtpEmailSender.cs',
     'message.Attachments is { Count: > 0 }',
     'mail.Attachments.Add')
must('backend/Garmetix.Api/GstReturns/GstReturnDtos.cs',
     'GstReturnReviewSendRequest',
     'GstReturnReviewSendResponse',
     'GstReportReviewSendRequest',
     'GstReportReviewSendResponse')
must('backend/Garmetix.Api/GstReturns/GstReturnEndpoints.cs',
     '/drafts/{id:guid}/send-review',
     '/reports/send-review',
     'SendDraftReviewAsync',
     'SendGstReportsReviewAsync',
     'BuildHsnSummaryReportAsync',
     'BuildTaxRateSummaryReportAsync',
     'BuildInvoiceRegisterReportAsync',
     'BuildWhatsAppShareUrl',
     'Shared for CA Review')
must('frontend/garmetix-web/pages/gst-returns/index.vue',
     'GST Review & CA Sharing',
     'sendReviewPackage',
     'Confirm & Send Email',
     'reviewShare.includeJson',
     'openWhatsAppShare')
must('frontend/garmetix-web/pages/gst-reports/index.vue',
     'GST Report Sharing',
     'sendGstReports',
     'Confirm & Send Reports',
     'openReportWhatsApp',
     'overflow-x: auto; overflow-y: visible;')
must('frontend/garmetix-web/utils/appVersion.ts',
     "APP_VERSION = '4.7.9'",
     'Stage 8H Package 10 GST Review Share Automation',
     'GARMETIX-8H-20260618-4790')
must('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
     'Version = "4.7.9"',
     'Stage 8H Package 10 GST Review Share Automation',
     'GARMETIX-8H-20260618-4790')
for rel in [
    'docs/stages/stage-8/Stage8H-Package10-GstReviewShareAutomation-v4.7.9-Notes.md',
    'docs/operations/GST-Review-Share-Automation-v4.7.9.md'
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')
print('Stage 8H Package 10 static validation passed.')
