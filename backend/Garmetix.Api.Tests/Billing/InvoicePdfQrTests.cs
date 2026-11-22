using System.Text;
using Garmetix.Api.Billing;
using Garmetix.Api.ProductLookup;
using Xunit;

namespace Garmetix.Api.Tests.Billing;

public sealed class InvoicePdfQrTests
{
    private static InvoicePdfModel Model() => new(
        "Aadwika Fashion", "Dumka", "9000000000", "20ABCDE1234F1Z5", "Main Store",
        "AF/2026/0001", new DateTime(2026, 11, 22, 10, 30, 0), "Paid",
        "Walk-in Customer", "0000000000",
        1000m, 100m, 857m, 43m, 0m, 900m, 900m, 0m,
        [new ReceiptItemDto(Guid.NewGuid(), null, "Shirt", "BC001", 1m, 1000m, 100m, 5m, 43m, 21.5m, 21.5m, 0m, "6205", "PCS", 900m)],
        [new ReceiptPaymentDto(Guid.NewGuid(), DateTime.Now, 900m, "Cash", null, null, null, null)],
        DocumentCodeService.Create(DocumentCodeService.SaleInvoice, Guid.NewGuid()));

    // A real QR is drawn as one filled rectangle per dark module (hundreds of them). The old placeholder drew a single
    // empty box with "SCAN CODE" text, so it produced almost no fill operations.
    private static int FillCount(byte[] pdf) => Encoding.Latin1.GetString(pdf).Split(" re f").Length - 1;

    [Theory]
    [InlineData("a4")]
    [InlineData("a5")]
    [InlineData("thermal-2")]
    [InlineData("thermal-3")]
    public void Every_invoice_format_draws_a_real_qr(string format)
    {
        var pdf = InvoicePdfDocument.Build(Model(), format, "customer", reprint: false, signatures: true);
        Assert.True(FillCount(pdf) > 150, $"{format} invoice should contain a real QR code but only had {FillCount(pdf)} filled shapes.");
        Assert.DoesNotContain("SCAN", Encoding.Latin1.GetString(pdf).Replace("Scan:", string.Empty));
    }

    [Theory]
    [InlineData("thermal-2")]
    [InlineData("thermal-3")]
    public void Thermal_invoice_has_invoice_qr_and_instagram_qr(string format)
    {
        var thermal = InvoicePdfDocument.Build(Model(), format, "customer", reprint: false, signatures: true);
        var a4 = InvoicePdfDocument.Build(Model(), "a4", "customer", reprint: false, signatures: true);
        var text = Encoding.Latin1.GetString(thermal);

        Assert.Contains("@aadwikafashion", text);
        Assert.Contains("Scan: AF/2026/0001", text);
        // Two QR codes on the receipt versus one on A4.
        Assert.True(FillCount(thermal) > FillCount(a4) + 150);
    }
}

public sealed class InvoicePdfTitleTests
{
    private static InvoicePdfModel Model(string title) => new(
        "Aadwika Fashion", "Dumka", "9000000000", "20ABCDE1234F1Z5", "Main Store",
        "SR-2026-0001", new DateTime(2026, 11, 22, 10, 30, 0), "Refunded",
        "Walk-in Customer", "0000000000",
        1000m, 100m, 857m, 43m, 0m, 900m, 900m, 0m,
        [new ReceiptItemDto(Guid.NewGuid(), null, "Shirt", "BC001", 1m, 1000m, 100m, 5m, 43m, 21.5m, 21.5m, 0m, "6205", "PCS", 900m)],
        [],
        DocumentCodeService.Create(DocumentCodeService.SaleInvoice, Guid.NewGuid()),
        DocumentTitle: title);

    [Theory]
    [InlineData("a4")]
    [InlineData("a5")]
    [InlineData("thermal-2")]
    [InlineData("thermal-3")]
    public void Return_invoice_prints_as_credit_note_in_every_format(string format)
    {
        var text = System.Text.Encoding.Latin1.GetString(InvoicePdfDocument.Build(Model("CREDIT NOTE"), format, "customer", false, true));
        Assert.Contains("CREDIT NOTE", text);
        Assert.DoesNotContain("TAX INVOICE", text);
    }

    [Fact]
    public void Normal_invoice_still_prints_as_tax_invoice()
    {
        var text = System.Text.Encoding.Latin1.GetString(InvoicePdfDocument.Build(Model("TAX INVOICE"), "a4", "customer", false, true));
        Assert.Contains("TAX INVOICE", text);
    }
}
