using Garmetix.Api.Auth;
using Garmetix.Core.Models.Configuration;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Configuration;

public sealed record SettingDto(
    Guid Id,
    string Key,
    string Category,
    string? DisplayName,
    string? Description,
    string? Value,
    DateTime? UpdatedAt,
    string? UpdatedByUserName);

public sealed record SettingCreateRequest(string Key, string Category, string? DisplayName, string? Description, string? Value);
public sealed record SettingUpdateRequest(string? Value);

/// <summary>
/// Settings page backend - client-facing, non-env, non-restart-requiring preferences (unlike
/// Configuration, these are free-form: any Admin/Owner can define a new setting key, not just
/// the fixed catalog ConfigCatalog defines). Applied immediately on save, nothing to write to
/// disk or restart. Same garmetix_config_db, distinguished by Scope.Setting.
/// </summary>
public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/settings")
            .WithTags("Admin Settings")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{key}", UpdateAsync);
        group.MapDelete("/{key}", DeleteAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(GarmetixConfigDbContext db, CancellationToken cancellationToken)
    {
        var entries = await db.ConfigEntries.AsNoTracking()
            .Where(e => e.Scope == ConfigScope.Setting)
            .OrderBy(e => e.Category).ThenBy(e => e.DisplayName ?? e.Key)
            .ToListAsync(cancellationToken);

        return Results.Ok(entries.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateAsync(SettingCreateRequest request, HttpContext context, GarmetixConfigDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Category))
        {
            return Results.BadRequest(new { message = "Key and Category are required." });
        }

        var normalizedKey = request.Key.Trim();
        var exists = await db.ConfigEntries.AnyAsync(e => e.Key == normalizedKey, cancellationToken);
        if (exists)
        {
            return Results.Conflict(new { message = $"A setting with key '{normalizedKey}' already exists." });
        }

        var entry = new ConfigEntry
        {
            Key = normalizedKey,
            Category = request.Category.Trim(),
            DisplayName = request.DisplayName?.Trim(),
            Description = request.Description?.Trim(),
            Scope = ConfigScope.Setting,
            IsSecret = false,
            PlainValue = request.Value,
            WriteToApiEnv = false,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByUserName = context.User.Identity?.Name
        };

        db.ConfigEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/admin/settings/{entry.Key}", ToDto(entry));
    }

    private static async Task<IResult> UpdateAsync(string key, SettingUpdateRequest request, HttpContext context, GarmetixConfigDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.ConfigEntries.FirstOrDefaultAsync(e => e.Key == key && e.Scope == ConfigScope.Setting, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound();
        }

        entry.PlainValue = request.Value;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.UpdatedByUserName = context.User.Identity?.Name;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDto(entry));
    }

    private static async Task<IResult> DeleteAsync(string key, GarmetixConfigDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.ConfigEntries.FirstOrDefaultAsync(e => e.Key == key && e.Scope == ConfigScope.Setting, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound();
        }

        entry.Deleted = true;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static SettingDto ToDto(ConfigEntry entry) => new(
        entry.Id,
        entry.Key,
        entry.Category,
        entry.DisplayName,
        entry.Description,
        entry.PlainValue,
        entry.UpdatedAt,
        entry.UpdatedByUserName);
}
