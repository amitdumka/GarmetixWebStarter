using System.Globalization;
using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Core.Models.GstTax;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>
/// Local HSN/SAC master - the first-class, API-independent source used for daily billing (spec section 1.5/6.4).
/// Bulk import accepts CSV rather than a real .xlsx parser: this codebase has no Excel-reading library, and a
/// hand-rolled OpenXML parser for arbitrary uploaded workbooks is a real correctness risk for a data-load path.
/// CSV covers the same "bulk load a HSN directory" need with far less risk; documented as a deliberate simplification.
/// </summary>
public static class GstHsnEndpoints
{
    private static readonly string[] CsvHeader =
    [
        "HsnCode", "CodeType", "ChapterCode", "Description", "TechnicalDescription", "CommonTradeDescription",
        "DefaultUqc", "DefaultGstRate", "CgstRate", "SgstRate", "IgstRate", "CessRate", "Source", "IsActive"
    ];

    public static RouteGroupBuilder MapGstHsnEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst/hsn")
            .WithTags("GST & Taxes - HSN Master")
            .RequireAuthorization(GarmetixPolicies.Gst);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(GarmetixPolicies.Delete);
        group.MapPost("/search", SearchAsync);
        group.MapPost("/bulk-status", BulkStatusAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/template", DownloadTemplate);
        group.MapPost("/import-csv", ImportCsvAsync).RequireAuthorization(GarmetixPolicies.Edit).DisableAntiforgery();

        return group;
    }

    private static async Task EnsureStorageAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairGstTaxStorageAsync(db, loggerFactory.CreateLogger("GstTaxStorageRepair"), cancellationToken);
    }

    private static async Task<IResult> ListAsync(
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        string? hsnCode = null,
        string? description = null,
        string? codeType = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 50)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var query = db.GstHsnMasters.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(hsnCode))
        {
            query = query.Where(h => h.HsnCode.Contains(hsnCode));
        }
        if (!string.IsNullOrWhiteSpace(description))
        {
            var term = description.ToLower();
            query = query.Where(h =>
                (h.Description != null && h.Description.ToLower().Contains(term)) ||
                (h.CommonTradeDescription != null && h.CommonTradeDescription.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(codeType))
        {
            query = query.Where(h => h.CodeType == codeType);
        }
        if (isActive.HasValue)
        {
            query = query.Where(h => h.IsActive == isActive.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(h => h.HsnCode)
            .Skip(Math.Max(0, (page - 1) * Math.Clamp(pageSize, 1, 500)))
            .Take(Math.Clamp(pageSize, 1, 500))
            .ToListAsync(cancellationToken);

        var codes = rows.Select(h => h.HsnCode).ToArray();
        var mappingCounts = await db.Products.AsNoTracking()
            .Where(p => !p.Deleted && p.HSNCode != null && codes.Contains(p.HSNCode))
            .GroupBy(p => p.HSNCode)
            .Select(g => new { HsnCode = g.Key!, Count = g.Count() })
            .ToDictionaryAsync(x => x.HsnCode, x => x.Count, cancellationToken);

        var result = rows.Select(h => ToRowDto(h, mappingCounts.GetValueOrDefault(h.HsnCode, 0)));
        return Results.Ok(new { total, page, pageSize, items = result });
    }

    private static async Task<IResult> SearchAsync(
        GstHsnSearchRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var term = (request.Query ?? string.Empty).Trim();
        var query = db.GstHsnMasters.AsNoTracking().Where(h => h.IsActive);

        if (request.GoodsOnly)
        {
            query = query.Where(h => h.CodeType == "Goods");
        }
        else if (request.ServicesOnly)
        {
            query = query.Where(h => h.CodeType == "Service");
        }

        if (!string.IsNullOrEmpty(term))
        {
            var lower = term.ToLower();
            query = query.Where(h =>
                h.HsnCode.Contains(term) ||
                (h.Description != null && h.Description.ToLower().Contains(lower)) ||
                (h.CommonTradeDescription != null && h.CommonTradeDescription.ToLower().Contains(lower)));
        }

        var rows = await query.OrderBy(h => h.HsnCode).Take(20).ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(h => ToRowDto(h, 0)));
    }

    private static async Task<IResult> CreateAsync(
        GstHsnSaveRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var validation = Validate(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        if (await db.GstHsnMasters.AsNoTracking().AnyAsync(h => h.HsnCode == request.HsnCode.Trim(), cancellationToken))
        {
            return Results.Conflict(new { message = $"HSN/SAC code '{request.HsnCode}' already exists." });
        }

        var entity = FromRequest(new GstHsnMaster(), request);
        db.GstHsnMasters.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToRowDto(entity, 0));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        GstHsnSaveRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var validation = Validate(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var entity = await db.GstHsnMasters.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "HSN/SAC entry not found." });
        }

        if (await db.GstHsnMasters.AsNoTracking().AnyAsync(h => h.Id != id && h.HsnCode == request.HsnCode.Trim(), cancellationToken))
        {
            return Results.Conflict(new { message = $"HSN/SAC code '{request.HsnCode}' already exists." });
        }

        FromRequest(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var count = await db.Products.AsNoTracking().CountAsync(p => !p.Deleted && p.HSNCode == entity.HsnCode, cancellationToken);
        return Results.Ok(ToRowDto(entity, count));
    }

    private static async Task<IResult> DeleteAsync(Guid id, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var entity = await db.GstHsnMasters.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "HSN/SAC entry not found." });
        }

        db.GstHsnMasters.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = "HSN/SAC entry deleted." });
    }

    private static async Task<IResult> BulkStatusAsync(
        GstHsnBulkStatusRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var rows = await db.GstHsnMasters.Where(h => request.Ids.Contains(h.Id)).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.IsActive = request.IsActive;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = $"{rows.Count} HSN/SAC entries updated.", updated = rows.Count });
    }

    private static IResult DownloadTemplate()
    {
        var csv = string.Join(",", CsvHeader) + "\n" +
                  "6203,Goods,62,\"Men's suits, jackets etc\",,,PCS,5,2.5,2.5,5,0,GST Import Template,true\n";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "garmetix-hsn-import-template.csv");
    }

    private static async Task<IResult> ImportCsvAsync(
        HttpRequest httpRequest,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        if (!httpRequest.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Upload the CSV as multipart/form-data with field name 'file'." });
        }

        var form = await httpRequest.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "No file uploaded." });
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (headerLine is null)
        {
            return Results.BadRequest(new { message = "The file is empty." });
        }

        var headers = SplitCsvLine(headerLine).Select(h => h.Trim()).ToArray();
        var errors = new List<string>();
        int rowsRead = 0, created = 0, updated = 0, skipped = 0;
        var lineNumber = 1;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            rowsRead++;
            var values = SplitCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length && i < values.Length; i++)
            {
                row[headers[i]] = values[i];
            }

            var hsnCode = row.GetValueOrDefault("HsnCode", "").Trim();
            if (string.IsNullOrEmpty(hsnCode))
            {
                skipped++;
                errors.Add($"Line {lineNumber}: missing HsnCode.");
                continue;
            }

            var entity = await db.GstHsnMasters.FirstOrDefaultAsync(h => h.HsnCode == hsnCode, cancellationToken);
            var isNew = entity is null;
            entity ??= new GstHsnMaster { HsnCode = hsnCode };

            entity.CodeType = NonEmptyOr(row.GetValueOrDefault("CodeType"), "Goods");
            entity.ChapterCode = NullIfEmpty(row.GetValueOrDefault("ChapterCode"));
            entity.Description = NullIfEmpty(row.GetValueOrDefault("Description"));
            entity.TechnicalDescription = NullIfEmpty(row.GetValueOrDefault("TechnicalDescription"));
            entity.CommonTradeDescription = NullIfEmpty(row.GetValueOrDefault("CommonTradeDescription"));
            entity.DefaultUqc = NullIfEmpty(row.GetValueOrDefault("DefaultUqc"));
            entity.DefaultGstRate = ParseDecimalOrNull(row.GetValueOrDefault("DefaultGstRate"));
            entity.CgstRate = ParseDecimalOrNull(row.GetValueOrDefault("CgstRate"));
            entity.SgstRate = ParseDecimalOrNull(row.GetValueOrDefault("SgstRate"));
            entity.IgstRate = ParseDecimalOrNull(row.GetValueOrDefault("IgstRate"));
            entity.CessRate = ParseDecimalOrNull(row.GetValueOrDefault("CessRate"));
            entity.Source = NullIfEmpty(row.GetValueOrDefault("Source")) ?? "CSV Import";
            entity.IsActive = !string.Equals(row.GetValueOrDefault("IsActive"), "false", StringComparison.OrdinalIgnoreCase);

            if (isNew)
            {
                db.GstHsnMasters.Add(entity);
                created++;
            }
            else
            {
                entity.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new GstHsnImportResultDto(rowsRead, created, updated, skipped, errors));
    }

    private static string[] SplitCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        values.Add(current.ToString());
        return values.Select(v => v.Trim().Trim('"')).ToArray();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NonEmptyOr(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static decimal? ParseDecimalOrNull(string? value) =>
        !string.IsNullOrWhiteSpace(value) && decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string? Validate(GstHsnSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.HsnCode))
        {
            return "HSN/SAC code is required.";
        }

        if (request.CodeType != "Goods" && request.CodeType != "Service")
        {
            return "Code type must be 'Goods' or 'Service'.";
        }

        return null;
    }

    private static GstHsnMaster FromRequest(GstHsnMaster entity, GstHsnSaveRequest request)
    {
        entity.HsnCode = request.HsnCode.Trim();
        entity.CodeType = request.CodeType;
        entity.ChapterCode = request.ChapterCode;
        entity.Description = request.Description;
        entity.TechnicalDescription = request.TechnicalDescription;
        entity.CommonTradeDescription = request.CommonTradeDescription;
        entity.DefaultUqc = request.DefaultUqc;
        entity.DefaultGstRate = request.DefaultGstRate;
        entity.CgstRate = request.CgstRate;
        entity.SgstRate = request.SgstRate;
        entity.IgstRate = request.IgstRate;
        entity.CessRate = request.CessRate;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.Source = request.Source;
        entity.IsActive = request.IsActive;
        return entity;
    }

    private static GstHsnRowDto ToRowDto(GstHsnMaster h, int mappingCount) => new(
        h.Id, h.HsnCode, h.CodeType, h.ChapterCode, h.Description, h.TechnicalDescription, h.CommonTradeDescription,
        h.DefaultUqc, h.DefaultGstRate, h.CgstRate, h.SgstRate, h.IgstRate, h.CessRate, h.EffectiveFrom, h.EffectiveTo,
        h.Source, h.IsActive, mappingCount);
}
