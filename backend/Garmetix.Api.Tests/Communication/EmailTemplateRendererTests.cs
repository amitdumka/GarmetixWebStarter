using Garmetix.Api.Communication;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

public sealed class EmailTemplateRendererTests
{
    [Fact]
    public void Render_SubstitutesTokensInSubjectHtmlAndText()
    {
        var tokens = new Dictionary<string, string> { ["customerName"] = "Priya", ["invoiceNumber"] = "INV-42" };
        var result = EmailTemplateRenderer.Render(
            "Invoice {{invoiceNumber}} for {{customerName}}",
            "<p>Dear {{customerName}}, invoice {{invoiceNumber}} is ready.</p>",
            "Dear {{customerName}}, invoice {{invoiceNumber}} is ready.",
            tokens);

        Assert.Equal("Invoice INV-42 for Priya", result.Subject);
        Assert.Contains("Dear Priya, invoice INV-42 is ready.", result.HtmlBody);
        Assert.Equal("Dear Priya, invoice INV-42 is ready.", result.TextBody);
    }

    [Fact]
    public void Render_HtmlEscapesTokenValues_PreventingInjectionFromMergeData()
    {
        var tokens = new Dictionary<string, string> { ["customerName"] = "<script>alert(1)</script>" };
        var result = EmailTemplateRenderer.Render("Hello {{customerName}}", "<p>Dear {{customerName}}</p>", null, tokens);

        Assert.DoesNotContain("<script>", result.HtmlBody);
        Assert.Contains("&lt;script&gt;", result.HtmlBody);
    }

    [Fact]
    public void Render_SanitizesScriptTagsFromAdminAuthoredTemplateHtmlItself()
    {
        var tokens = new Dictionary<string, string>();
        var result = EmailTemplateRenderer.Render(
            "Subject",
            "<p>Hello</p><script>alert('xss')</script><img src=x onerror=\"alert(1)\">",
            null,
            tokens);

        Assert.DoesNotContain("<script", result.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", result.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello", result.HtmlBody);
    }

    [Fact]
    public void Render_LeavesUnresolvableTokensVisibleRatherThanSilentlyBlanking()
    {
        var result = EmailTemplateRenderer.Render("Hi {{unknownToken}}", "<p>{{unknownToken}}</p>", null, new Dictionary<string, string>());
        Assert.Contains("{{unknownToken}}", result.Subject);
        Assert.Contains("{{unknownToken}}", result.HtmlBody);
    }

    [Fact]
    public void ParseSampleData_ParsesFlatJsonObjectIntoStringDictionary()
    {
        var tokens = EmailTemplateRenderer.ParseSampleData("""{"customerName":"Priya","amount":"Rs 500"}""");
        Assert.Equal("Priya", tokens["customerName"]);
        Assert.Equal("Rs 500", tokens["amount"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    public void ParseSampleData_ReturnsEmptyDictionary_ForInvalidOrMissingInput(string? input)
    {
        var tokens = EmailTemplateRenderer.ParseSampleData(input);
        Assert.Empty(tokens);
    }
}
