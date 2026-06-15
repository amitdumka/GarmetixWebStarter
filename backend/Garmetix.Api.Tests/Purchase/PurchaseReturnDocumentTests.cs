using System.Text;
using Garmetix.Api.ProductLookup;
using Garmetix.Api.Purchase;
using Xunit;

namespace Garmetix.Api.Tests.Purchase;

public sealed class PurchaseReturnDocumentTests
{
    [Fact]
    public void PurchaseReturnCodeRoundTrips()
    {
        var id = Guid.NewGuid();
        var code = DocumentCodeService.Create(DocumentCodeService.PurchaseReturn, id);

        Assert.True(DocumentCodeService.TryParse(code, out var type, out var parsedId));
        Assert.Equal(DocumentCodeService.PurchaseReturn, type);
        Assert.Equal(id, parsedId);
    }

    [Theory]
    [InlineData("a4")]
    [InlineData("a5")]
    public void PurchaseReturnPdfContainsAllPagesAndQrPayload(string format)
    {
        var id = Guid.NewGuid();
        var code = DocumentCodeService.Create(DocumentCodeService.PurchaseReturn, id);
        var items = Enumerable.Range(1, 20)
            .Select(index => new PurchaseReturnItemDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"Returned item {index}",
                $"BC{index:000}",
                "6203",
                "Pcs",
                10,
                0,
                1,
                999,
                500,
                0,
                500,
                12,
                60,
                30,
                30,
                0,
                560,
                "Damaged"))
            .ToList();
        var detail = new PurchaseReturnDetailDto(
            id,
            "STR/202606/PR/0001",
            new DateTime(2026, 6, 15),
            "Partial",
            "Posted",
            Guid.NewGuid(),
            "STR/202606/P/0010",
            new DateTime(2026, 6, 1),
            new DateTime(2026, 5, 31),
            Guid.NewGuid(),
            "Example Vendor",
            "20ABCDE1234F1Z5",
            20,
            10_000,
            1_200,
            600,
            600,
            0,
            11_200,
            Guid.NewGuid(),
            "STR/202606/DN/0001",
            "Damaged goods returned",
            false,
            0,
            null,
            "Not Printed",
            items);

        var pdf = PurchaseReturnPdfDocument.Build(
            new PurchaseReturnPdfModel(
                detail,
                "Garmetix Test Company",
                "Test Address",
                "9999999999",
                "20ABCDE1234F1Z5",
                "Main Store",
                code),
            format,
            "store",
            false,
            true);

        var text = Encoding.ASCII.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("PURCHASE RETURN", text);
        Assert.Contains("DEBIT NOTE", text);
        Assert.Contains("Returned item 20", text);
        Assert.True(pdf.Length > 5_000);
    }
}
