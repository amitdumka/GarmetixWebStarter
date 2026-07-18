using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

public sealed record BusinessEmailResult(bool Enqueued, string? SkipReason);

/// <summary>
/// CM-08 business integration: one method per low-risk, additive integration point named in
/// the master prompt (Sales, Purchase, HR/Payroll, Inventory, Administration). Every method
/// only enqueues (via EmailEnqueueService) - it never calls a provider directly, and it is
/// always called from a NEW sibling endpoint added after an existing business transaction has
/// already committed, never from inside that transaction (see docs/communication-mail-architecture.md
/// and the CM-08 commit notes for the exact call-site survey this was built from). A missing
/// recipient email or missing published template is treated as a soft skip (BusinessEmailResult
/// with Enqueued=false), never an exception - this integration must never be able to break the
/// business action a caller just performed.
/// </summary>
public sealed class BusinessNotificationService(GarmetixDbContext db, EmailEnqueueService enqueueService)
{
    public async Task<BusinessEmailResult> SendSaleInvoiceEmailAsync(
        Guid invoiceId, string customerEmail, string? customerName, IReadOnlyDictionary<string, string> tokens,
        Guid? companyId, Guid? storeGroupId, Guid? storeId, Guid? createdByUserId, byte[]? invoicePdf, string? invoicePdfFileName,
        CancellationToken cancellationToken) =>
        await EnqueueFromTemplateAsync("sale-invoice", "Sales", "Invoice", invoiceId, customerEmail, customerName, tokens,
            companyId, storeGroupId, storeId, createdByUserId, invoicePdf, invoicePdfFileName, cancellationToken);

    public async Task<BusinessEmailResult> SendVendorPaymentEmailAsync(
        Guid paymentSourceId, string vendorEmail, string? vendorName, IReadOnlyDictionary<string, string> tokens,
        Guid? companyId, Guid? storeGroupId, Guid? storeId, Guid? createdByUserId, CancellationToken cancellationToken) =>
        await EnqueueFromTemplateAsync("vendor-payment", "Purchase", "VendorPayment", paymentSourceId, vendorEmail, vendorName, tokens,
            companyId, storeGroupId, storeId, createdByUserId, null, null, cancellationToken);

    public async Task<BusinessEmailResult> SendPayslipEmailAsync(
        Guid payslipId, string employeeEmail, string? employeeName, IReadOnlyDictionary<string, string> tokens,
        Guid? companyId, Guid? storeGroupId, Guid? storeId, Guid? createdByUserId, byte[]? payslipPdf, string? payslipPdfFileName,
        CancellationToken cancellationToken) =>
        await EnqueueFromTemplateAsync("payslip", "HR", "Payslip", payslipId, employeeEmail, employeeName, tokens,
            companyId, storeGroupId, storeId, createdByUserId, payslipPdf, payslipPdfFileName, cancellationToken);

    public async Task<BusinessEmailResult> SendLowStockDigestEmailAsync(
        Guid digestSourceId, string recipientEmail, string? recipientName, IReadOnlyDictionary<string, string> tokens,
        Guid? companyId, Guid? storeGroupId, Guid? storeId, Guid? createdByUserId, CancellationToken cancellationToken) =>
        await EnqueueFromTemplateAsync("low-stock", "Inventory", "LowStockDigest", digestSourceId, recipientEmail, recipientName, tokens,
            companyId, storeGroupId, storeId, createdByUserId, null, null, cancellationToken);

    public async Task<BusinessEmailResult> SendUserInvitationEmailAsync(
        Guid userId, string userEmail, string? userName, IReadOnlyDictionary<string, string> tokens,
        Guid? companyId, Guid? storeGroupId, Guid? storeId, Guid? createdByUserId, CancellationToken cancellationToken) =>
        await EnqueueFromTemplateAsync("invitation", "Administration", "UserInvitation", userId, userEmail, userName, tokens,
            companyId, storeGroupId, storeId, createdByUserId, null, null, cancellationToken);

    private async Task<BusinessEmailResult> EnqueueFromTemplateAsync(
        string templateKey,
        string sourceModule,
        string sourceType,
        Guid sourceId,
        string? recipientEmail,
        string? recipientName,
        IReadOnlyDictionary<string, string> tokens,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        Guid? createdByUserId,
        byte[]? attachmentBytes,
        string? attachmentFileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return new BusinessEmailResult(false, "No recipient email address on file.");
        }

        var template = await db.EmailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CompanyId == null && t.TemplateKey == templateKey && t.IsActive, cancellationToken);
        if (template?.CurrentVersionId is null)
        {
            return new BusinessEmailResult(false, $"Template '{templateKey}' has no published version.");
        }

        var version = await db.EmailTemplateVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == template.CurrentVersionId, cancellationToken);
        if (version is null)
        {
            return new BusinessEmailResult(false, "Template version could not be loaded.");
        }

        var rendered = EmailTemplateRenderer.Render(version.Subject, version.HtmlBody, version.TextBody, tokens);
        var attachments = attachmentBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(attachmentFileName)
            ? new List<EmailAttachmentPayload> { new(attachmentFileName, "application/pdf", attachmentBytes) }
            : null;

        var request = new EmailEnqueueRequest(
            companyId, storeGroupId, storeId,
            sourceModule, sourceType, sourceId,
            templateKey, template.Id, version.Id,
            rendered.Subject, rendered.HtmlBody, rendered.TextBody,
            [new EmailAddressValue(recipientEmail, recipientName)],
            Revision: 1,
            createdByUserId,
            Attachments: attachments);

        await enqueueService.EnqueueAsync(request, cancellationToken);
        return new BusinessEmailResult(true, null);
    }
}
