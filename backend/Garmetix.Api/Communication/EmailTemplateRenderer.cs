using System.Text.Json;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace Garmetix.Api.Communication;

public sealed record EmailTemplateRenderResult(string Subject, string HtmlBody, string? TextBody);

/// <summary>
/// Renders an EmailTemplateVersion's Subject/HtmlBody/TextBody against a merge-token dictionary.
/// Two independent layers of protection, per docs/communication-mail-security.md:
/// 1. Every token VALUE is HTML-escaped before substitution, so a customer name or address
///    containing "&lt;script&gt;" can never inject markup, regardless of what the template author wrote.
/// 2. The final rendered HTML is passed through HtmlSanitizer (an allow-list sanitizer, not a
///    hand-rolled regex blacklist) to strip script tags/event handlers/dangerous attributes even
///    from administrator-authored template HTML itself.
/// </summary>
public static partial class EmailTemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*(?<token>[A-Za-z0-9_.]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex TokenPattern();

    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    public static EmailTemplateRenderResult Render(string subjectTemplate, string htmlTemplate, string? textTemplate, IReadOnlyDictionary<string, string> tokens)
    {
        var renderedSubject = SubstituteTokens(subjectTemplate, tokens, escapeHtml: false);
        var renderedHtml = Sanitizer.Sanitize(SubstituteTokens(htmlTemplate, tokens, escapeHtml: true));
        var renderedText = textTemplate is null ? null : SubstituteTokens(textTemplate, tokens, escapeHtml: false);
        return new EmailTemplateRenderResult(renderedSubject, renderedHtml, renderedText);
    }

    /// <summary>Parses SampleDataJson (a flat string-keyed JSON object) into a token dictionary for preview/test-send.</summary>
    public static IReadOnlyDictionary<string, string> ParseSampleData(string? sampleDataJson)
    {
        if (string.IsNullOrWhiteSpace(sampleDataJson))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sampleDataJson);
            if (parsed is null)
            {
                return new Dictionary<string, string>();
            }

            return parsed.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() ?? string.Empty : pair.Value.ToString());
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static string SubstituteTokens(string template, IReadOnlyDictionary<string, string> tokens, bool escapeHtml)
    {
        return TokenPattern().Replace(template, match =>
        {
            var tokenName = match.Groups["token"].Value;
            if (!tokens.TryGetValue(tokenName, out var value))
            {
                return match.Value; // leave un-resolvable tokens visible rather than silently blanking them
            }

            return escapeHtml ? System.Net.WebUtility.HtmlEncode(value) : value;
        });
    }

    private static HtmlSanitizer BuildSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowedAttributes.Add("style");
        // Templates are plain marketing/transactional HTML - never need scripts, forms, or embeds.
        sanitizer.AllowedTags.Remove("script");
        sanitizer.AllowedTags.Remove("iframe");
        sanitizer.AllowedTags.Remove("object");
        sanitizer.AllowedTags.Remove("embed");
        sanitizer.AllowedTags.Remove("form");
        return sanitizer;
    }
}
