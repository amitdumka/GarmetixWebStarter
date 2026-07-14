using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsExchangeRules
{
    public const string NoDirectTallyPosting = "NoDirectProductionTallyPosting";

    public static FinalAccountsTallyDuplicatePolicy ParseDuplicatePolicy(string? value)
        => Enum.TryParse<FinalAccountsTallyDuplicatePolicy>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Tally duplicate policy '{value}' is not supported.");

    public static FinalAccountsExchangeRunKind ParseRunKind(string? value)
        => Enum.TryParse<FinalAccountsExchangeRunKind>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Exchange run kind '{value}' is not supported.");

    public static string BuildRunNumberPrefix(FinalAccountsExchangeRunKind kind, DateTime periodEnd)
        => $"{(kind == FinalAccountsExchangeRunKind.CaPackage ? "CAPKG" : "TALLY")}-{periodEnd:yyyyMMdd}";

    public static string NormalizeFormat(string? value)
    {
        var format = (value ?? "zip").Trim().ToLowerInvariant();
        if (format is not ("zip" or "xml" or "json" or "csv"))
        {
            throw new ArgumentException("Exchange format must be zip, xml, json or csv.");
        }

        return format;
    }

    public static string NormalizeText(string? value, string fieldName, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"{fieldName} is required.");
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static string? OptionalText(string? value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static string ToJson(IReadOnlyDictionary<string, string>? mapping)
        => JsonSerializer.Serialize(mapping ?? new Dictionary<string, string>());

    public static IReadOnlyDictionary<string, string> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    public static string BuildTallyXmlFixture(
        string companyName,
        IReadOnlyList<FinalAccountsExchangeMappingRowDto> mappings,
        IReadOnlyList<TallyVoucherFixtureRow> vouchers)
    {
        var envelope = new XElement("ENVELOPE",
            new XElement("HEADER", new XElement("TALLYREQUEST", "Import Data")),
            new XElement("BODY",
                new XElement("IMPORTDATA",
                    new XElement("REQUESTDESC",
                        new XElement("REPORTNAME", "All Masters"),
                        new XElement("STATICVARIABLES", new XElement("SVCURRENTCOMPANY", companyName))),
                    new XElement("REQUESTDATA",
                        mappings.Take(25).Select(item => new XElement("TALLYMESSAGE",
                            new XElement("LEDGER",
                                new XAttribute("NAME", item.TallyName),
                                new XAttribute("ACTION", "Create"),
                                new XElement("PARENT", item.MappingType),
                                new XElement("GARMETIXSOURCE", item.SourceKey)))),
                        vouchers.Take(25).Select(item => new XElement("TALLYMESSAGE",
                            new XElement("VOUCHER",
                                new XAttribute("VCHTYPE", item.VoucherType),
                                new XAttribute("ACTION", "Create"),
                                new XElement("DATE", item.OnDate.ToString("yyyyMMdd")),
                                new XElement("VOUCHERNUMBER", item.Number),
                                new XElement("NARRATION", item.Narration),
                                new XElement("AMOUNT", item.Amount))))))));
        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    public static string BuildJsonFixture(object payload)
        => JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

    public static string Sha256Hex(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string Sha256Hex(string value)
        => Sha256Hex(Encoding.UTF8.GetBytes(value));

    public static decimal RoundAmount(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record TallyVoucherFixtureRow(string Number, DateTime OnDate, string VoucherType, string Narration, decimal Amount);
