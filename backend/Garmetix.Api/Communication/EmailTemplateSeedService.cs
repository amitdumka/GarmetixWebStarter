using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// One-time (idempotent, re-runnable) seed of the 14 initial system email templates named in
/// the master prompt. Each is a real, usable starting point (not filler text) that business
/// modules can reference by TemplateKey once CM-08 wires up the actual integrations - the
/// content here is what a template editor would produce, not a placeholder.
/// </summary>
public static class EmailTemplateSeedService
{
    private sealed record SeedTemplate(string Key, string DisplayName, string Category, string Subject, string HtmlBody, string TextBody, string SampleDataJson);

    public static async Task EnsureSeedDataAsync(GarmetixDbContext db, CancellationToken cancellationToken = default)
    {
        foreach (var seed in SeedTemplates)
        {
            var exists = await db.EmailTemplates.AsNoTracking()
                .AnyAsync(t => t.CompanyId == null && t.TemplateKey == seed.Key, cancellationToken);
            if (exists)
            {
                continue;
            }

            var template = new EmailTemplate
            {
                TemplateKey = seed.Key,
                DisplayName = seed.DisplayName,
                Category = seed.Category,
                IsSystemTemplate = true,
                IsActive = true,
            };
            db.EmailTemplates.Add(template);
            await db.SaveChangesAsync(cancellationToken);

            var version = new EmailTemplateVersion
            {
                TemplateId = template.Id,
                VersionNumber = 1,
                Subject = seed.Subject,
                HtmlBody = seed.HtmlBody,
                TextBody = seed.TextBody,
                SampleDataJson = seed.SampleDataJson,
                Status = EmailCatalog.TemplateVersionStatuses.Approved,
                ApprovedAtUtc = DateTime.UtcNow,
            };
            db.EmailTemplateVersions.Add(version);
            await db.SaveChangesAsync(cancellationToken);

            template.CurrentVersionId = version.Id;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static readonly SeedTemplate[] SeedTemplates =
    [
        new("sale-invoice", "Sale Invoice", "Sales",
            "Your invoice {{invoiceNumber}} from {{storeName}}",
            "<p>Dear {{customerName}},</p><p>Thank you for your purchase at <strong>{{storeName}}</strong>. Your invoice <strong>{{invoiceNumber}}</strong> dated {{invoiceDate}} for <strong>{{invoiceTotal}}</strong> is attached.</p><p>Regards,<br/>{{storeName}}</p>",
            "Dear {{customerName}}, thank you for your purchase at {{storeName}}. Invoice {{invoiceNumber}} dated {{invoiceDate}} for {{invoiceTotal}} is attached.",
            """{"customerName":"Customer","storeName":"Garmetix Store","invoiceNumber":"INV-0001","invoiceDate":"2026-01-01","invoiceTotal":"Rs 1,000.00"}"""),
        new("receipt", "Payment Receipt", "Sales",
            "Receipt for your payment - {{receiptNumber}}",
            "<p>Dear {{customerName}},</p><p>We have received your payment of <strong>{{amount}}</strong> on {{receiptDate}}. Receipt number: <strong>{{receiptNumber}}</strong>.</p><p>Regards,<br/>{{storeName}}</p>",
            "Dear {{customerName}}, we received your payment of {{amount}} on {{receiptDate}}. Receipt: {{receiptNumber}}.",
            """{"customerName":"Customer","storeName":"Garmetix Store","receiptNumber":"RCPT-0001","amount":"Rs 500.00","receiptDate":"2026-01-01"}"""),
        new("credit-note", "Credit Note", "Sales",
            "Credit note {{creditNoteNumber}} issued",
            "<p>Dear {{customerName}},</p><p>A credit note <strong>{{creditNoteNumber}}</strong> for <strong>{{amount}}</strong> has been issued against invoice {{invoiceNumber}}.</p><p>Regards,<br/>{{storeName}}</p>",
            "Dear {{customerName}}, credit note {{creditNoteNumber}} for {{amount}} has been issued against invoice {{invoiceNumber}}.",
            """{"customerName":"Customer","storeName":"Garmetix Store","creditNoteNumber":"CN-0001","amount":"Rs 200.00","invoiceNumber":"INV-0001"}"""),
        new("purchase-inward", "Purchase Inward Confirmation", "Purchase",
            "Inward confirmation for {{invoiceNumber}}",
            "<p>Dear {{vendorName}},</p><p>We confirm receipt of goods against your invoice <strong>{{invoiceNumber}}</strong> dated {{invoiceDate}} at {{storeName}}.</p><p>Regards,<br/>{{storeName}}</p>",
            "Dear {{vendorName}}, we confirm receipt of goods against invoice {{invoiceNumber}} dated {{invoiceDate}} at {{storeName}}.",
            """{"vendorName":"Vendor","storeName":"Garmetix Store","invoiceNumber":"PINV-0001","invoiceDate":"2026-01-01"}"""),
        new("vendor-payment", "Vendor Payment Confirmation", "Purchase",
            "Payment sent - {{amount}} against {{invoiceNumber}}",
            "<p>Dear {{vendorName}},</p><p>A payment of <strong>{{amount}}</strong> has been sent against invoice <strong>{{invoiceNumber}}</strong> on {{paymentDate}} via {{paymentMode}}.</p><p>Regards,<br/>{{storeName}}</p>",
            "Dear {{vendorName}}, a payment of {{amount}} has been sent against invoice {{invoiceNumber}} on {{paymentDate}} via {{paymentMode}}.",
            """{"vendorName":"Vendor","storeName":"Garmetix Store","amount":"Rs 5,000.00","invoiceNumber":"PINV-0001","paymentDate":"2026-01-01","paymentMode":"Bank Transfer"}"""),
        new("payment-reminder", "Payment Reminder", "Accounting",
            "Reminder: {{amount}} outstanding on invoice {{invoiceNumber}}",
            "<p>Dear {{customerName}},</p><p>This is a reminder that <strong>{{amount}}</strong> is outstanding against invoice <strong>{{invoiceNumber}}</strong> dated {{invoiceDate}}. Please arrange payment at your earliest convenience.</p><p>Regards,<br/>{{storeName}}</p>",
            "Dear {{customerName}}, {{amount}} is outstanding against invoice {{invoiceNumber}} dated {{invoiceDate}}. Please arrange payment at your earliest convenience.",
            """{"customerName":"Customer","storeName":"Garmetix Store","amount":"Rs 1,500.00","invoiceNumber":"INV-0001","invoiceDate":"2026-01-01"}"""),
        new("payslip", "Payslip Notification", "HR/Payroll",
            "Your payslip for {{payPeriod}} is ready",
            "<p>Dear {{employeeName}},</p><p>Your payslip for <strong>{{payPeriod}}</strong> is attached. Net pay: <strong>{{netSalary}}</strong>.</p><p>Regards,<br/>HR Team</p>",
            "Dear {{employeeName}}, your payslip for {{payPeriod}} is attached. Net pay: {{netSalary}}.",
            """{"employeeName":"Employee","payPeriod":"January 2026","netSalary":"Rs 30,000.00"}"""),
        new("attendance-notice", "Attendance Notice", "HR/Payroll",
            "Attendance notice - {{noticeDate}}",
            "<p>Dear {{employeeName}},</p><p>{{noticeMessage}}</p><p>Regards,<br/>HR Team</p>",
            "Dear {{employeeName}}, {{noticeMessage}}",
            """{"employeeName":"Employee","noticeDate":"2026-01-01","noticeMessage":"Your attendance for yesterday has been marked as absent. Please contact HR if this is incorrect."}"""),
        new("password-reset", "Password Reset", "Administration",
            "Reset your Garmetix password",
            "<p>Hello {{userName}},</p><p>Click the link below to reset your password. This link expires in {{expiryMinutes}} minutes.</p><p><a href=\"{{resetUrl}}\">Reset Password</a></p><p>If you did not request this, you can safely ignore this email.</p>",
            "Hello {{userName}}, reset your password here: {{resetUrl}} (expires in {{expiryMinutes}} minutes). If you did not request this, ignore this email.",
            """{"userName":"User","resetUrl":"https://example.com/reset?token=sample","expiryMinutes":"30"}"""),
        new("invitation", "User Invitation", "Administration",
            "You have been invited to Garmetix",
            "<p>Hello {{inviteeName}},</p><p>{{inviterName}} has invited you to join Garmetix as a <strong>{{role}}</strong>. Click below to accept.</p><p><a href=\"{{invitationUrl}}\">Accept Invitation</a></p>",
            "Hello {{inviteeName}}, {{inviterName}} invited you to join Garmetix as {{role}}. Accept here: {{invitationUrl}}",
            """{"inviteeName":"New User","inviterName":"Admin","role":"Store Manager","invitationUrl":"https://example.com/invite?token=sample"}"""),
        new("daily-summary", "Daily Business Summary", "Accounting",
            "Daily summary for {{storeName}} - {{summaryDate}}",
            "<p>Dear {{recipientName}},</p><p>Here is the daily summary for <strong>{{storeName}}</strong> on {{summaryDate}}:</p><ul><li>Sales: {{totalSales}}</li><li>Purchases: {{totalPurchases}}</li><li>Cash in hand: {{cashInHand}}</li></ul>",
            "Daily summary for {{storeName}} on {{summaryDate}}: Sales {{totalSales}}, Purchases {{totalPurchases}}, Cash in hand {{cashInHand}}.",
            """{"recipientName":"Owner","storeName":"Garmetix Store","summaryDate":"2026-01-01","totalSales":"Rs 50,000.00","totalPurchases":"Rs 20,000.00","cashInHand":"Rs 10,000.00"}"""),
        new("low-stock", "Low Stock Alert", "Inventory",
            "Low stock alert - {{productName}} at {{storeName}}",
            "<p>Dear {{recipientName}},</p><p><strong>{{productName}}</strong> at {{storeName}} has fallen below its reorder level. Current stock: <strong>{{currentStock}}</strong>, reorder level: {{reorderLevel}}.</p>",
            "Low stock: {{productName}} at {{storeName}} - current stock {{currentStock}}, reorder level {{reorderLevel}}.",
            """{"recipientName":"Store Manager","productName":"Sample Product","storeName":"Garmetix Store","currentStock":"5","reorderLevel":"20"}"""),
        new("system-alert", "System Alert", "Administration",
            "Garmetix system alert: {{alertTitle}}",
            "<p>Dear {{recipientName}},</p><p><strong>{{alertTitle}}</strong></p><p>{{alertMessage}}</p><p>Occurred at: {{occurredAt}}</p>",
            "System alert: {{alertTitle}} - {{alertMessage}} (occurred at {{occurredAt}})",
            """{"recipientName":"Admin","alertTitle":"Sample Alert","alertMessage":"This is a sample system alert message.","occurredAt":"2026-01-01 10:00"}"""),
        new("internal-message-notification", "Internal Message Notification", "Administration",
            "New message from {{senderName}}: {{subject}}",
            "<p>Dear {{recipientName}},</p><p>You have a new internal message from <strong>{{senderName}}</strong>:</p><p><em>{{subject}}</em></p><p><a href=\"{{messageUrl}}\">Open in Garmetix</a></p>",
            "New message from {{senderName}}: {{subject}}. Open it here: {{messageUrl}}",
            """{"recipientName":"User","senderName":"Colleague","subject":"Sample message subject","messageUrl":"https://example.com/communication"}"""),
    ];
}
