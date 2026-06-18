using System.Security.Claims;
using Garmetix.Api.Messages;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Authentication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Auth;

public static class UserManagementEndpoints
{
    public static RouteGroupBuilder MapUserManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/access/users")
            .WithTags("Access")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/", ListUsersAsync);
        group.MapPost("/", CreateUserAsync);
        group.MapPut("/{id:guid}", UpdateUserAsync);
        group.MapPost("/{id:guid}/status", SetUserStatusAsync);
        group.MapPost("/{id:guid}/reset-password", ResetUserPasswordAsync);
        group.MapDelete("/{id:guid}", DeleteUserAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IReadOnlyList<UserListItemDto>> ListUsersAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        return await db.Users
            .AsNoTracking()
            .OrderByDescending(user => user.IsActive)
            .ThenBy(user => user.Name)
            .Select(user => ToDto(user))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> CreateUserAsync(
        SaveUserRequest request,
        HttpContext context,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validation = ValidateUserRequest(request, requirePassword: true);
        if (validation is not null)
        {
            return validation;
        }

        var normalizedUserName = request.UserName.Trim();
        var normalizedEmail = request.Email.Trim();
        var exists = await db.Users.AnyAsync(
            user => user.UserName == normalizedUserName || user.Email == normalizedEmail,
            cancellationToken);

        if (exists)
        {
            return Results.Conflict(new { message = "A user with the same username or email already exists." });
        }

        var user = new AppUser
        {
            Name = request.Name.Trim(),
            UserName = normalizedUserName,
            Email = normalizedEmail,
            Password = PasswordHasher.Hash(request.Password!),
            Role = request.Role,
            UserType = request.UserType,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId,
            Admin = request.Role == LoginRole.Admin,
            IsActive = request.IsActive,
            AppOperation = request.AppOperation
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        await WriteSecurityEventAsync(
            logs,
            loggerFactory,
            context,
            "UserCreated",
            $"User {user.UserName} was created.",
            user,
            new { After = Snapshot(user) },
            cancellationToken);

        return Results.Created($"/api/access/users/{user.Id}", ToDto(user));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid id,
        SaveUserRequest request,
        HttpContext context,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validation = ValidateUserRequest(request, requirePassword: false);
        if (validation is not null)
        {
            return validation;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { message = "Use Reset Password to change another user's password." });
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        var normalizedUserName = request.UserName.Trim();
        var normalizedEmail = request.Email.Trim();
        var exists = await db.Users.AnyAsync(
            item => item.Id != id && (item.UserName == normalizedUserName || item.Email == normalizedEmail),
            cancellationToken);

        if (exists)
        {
            return Results.Conflict(new { message = "A user with the same username or email already exists." });
        }

        var actorId = CurrentUserId(context);
        if (!request.IsActive && actorId == user.Id)
        {
            return Results.Conflict(new { message = "You cannot deactivate your own signed-in account." });
        }

        var removesActiveAdmin = IsActiveAdmin(user)
            && (request.Role != LoginRole.Admin || !request.IsActive);
        if (removesActiveAdmin && await IsLastActiveAdminAsync(user.Id, db, cancellationToken))
        {
            return Results.Conflict(new { message = "Cannot remove or deactivate the last active admin user." });
        }

        var before = Snapshot(user);
        user.Name = request.Name.Trim();
        user.UserName = normalizedUserName;
        user.Email = normalizedEmail;
        user.Role = request.Role;
        user.UserType = request.UserType;
        user.CompanyId = request.CompanyId;
        user.StoreGroupId = request.StoreGroupId;
        user.StoreId = request.StoreId;
        user.Admin = request.Role == LoginRole.Admin;
        user.IsActive = request.IsActive;
        user.AppOperation = request.AppOperation;

        await db.SaveChangesAsync(cancellationToken);

        await WriteSecurityEventAsync(
            logs,
            loggerFactory,
            context,
            "UserUpdated",
            $"User {user.UserName} was updated.",
            user,
            new { Before = before, After = Snapshot(user) },
            cancellationToken);

        return Results.Ok(ToDto(user));
    }

    private static async Task<IResult> SetUserStatusAsync(
        Guid id,
        SetUserStatusRequest request,
        HttpContext context,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        if (user.IsActive == request.IsActive)
        {
            return Results.Ok(ToDto(user));
        }

        if (!request.IsActive && CurrentUserId(context) == user.Id)
        {
            return Results.Conflict(new { message = "You cannot deactivate your own signed-in account." });
        }

        if (!request.IsActive
            && IsActiveAdmin(user)
            && await IsLastActiveAdminAsync(user.Id, db, cancellationToken))
        {
            return Results.Conflict(new { message = "Cannot deactivate the last active admin user." });
        }

        var previousStatus = user.IsActive;
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        await WriteSecurityEventAsync(
            logs,
            loggerFactory,
            context,
            request.IsActive ? "UserActivated" : "UserDeactivated",
            $"User {user.UserName} was {(request.IsActive ? "activated" : "deactivated")}.",
            user,
            new { Before = previousStatus, After = user.IsActive },
            cancellationToken);

        return Results.Ok(ToDto(user));
    }

    private static async Task<IResult> ResetUserPasswordAsync(
        Guid id,
        AdminResetPasswordRequest request,
        HttpContext context,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return Results.BadRequest(new { message = "New password must be at least 6 characters." });
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        user.Password = PasswordHasher.Hash(request.NewPassword);
        await RevokeResetTokensAsync(user.Id, db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await WriteSecurityEventAsync(
            logs,
            loggerFactory,
            context,
            "UserPasswordReset",
            $"Password was administratively reset for {user.UserName}.",
            user,
            new { TargetUserId = user.Id, TargetUserName = user.UserName },
            cancellationToken);

        return Results.Ok(new { message = "Password reset successfully." });
    }

    private static async Task<IResult> DeleteUserAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId(context) == id)
        {
            return Results.Conflict(new { message = "You cannot delete your own signed-in user." });
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        if (IsActiveAdmin(user) && await IsLastActiveAdminAsync(user.Id, db, cancellationToken))
        {
            return Results.Conflict(new { message = "Cannot delete the last active admin user." });
        }

        var before = Snapshot(user);
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        await WriteSecurityEventAsync(
            logs,
            loggerFactory,
            context,
            "UserDeleted",
            $"User {user.UserName} was deleted.",
            user,
            new { Before = before },
            cancellationToken);

        return Results.NoContent();
    }

    private static IResult? ValidateUserRequest(SaveUserRequest request, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.UserName)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new { message = "Name, username, and email are required." });
        }

        if (requirePassword && string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { message = "Password is required for new users." });
        }

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 6)
        {
            return Results.BadRequest(new { message = "Password must be at least 6 characters." });
        }

        return null;
    }

    private static bool IsActiveAdmin(AppUser user)
        => user.IsActive && (user.Admin || user.Role == LoginRole.Admin);

    private static async Task<bool> IsLastActiveAdminAsync(Guid userId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var adminCount = await db.Users.CountAsync(
            user => user.IsActive && (user.Admin || user.Role == LoginRole.Admin),
            cancellationToken);
        var userIsAdmin = await db.Users.AnyAsync(
            user => user.Id == userId && user.IsActive && (user.Admin || user.Role == LoginRole.Admin),
            cancellationToken);

        return userIsAdmin && adminCount <= 1;
    }

    private static async Task RevokeResetTokensAsync(Guid userId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var tokens = await db.PasswordResetTokens
            .Where(token => token.UserId == userId && !token.UsedAtUtc.HasValue && !token.RevokedAtUtc.HasValue)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAtUtc = now;
        }
    }

    private static Guid? CurrentUserId(HttpContext context)
        => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static object Snapshot(AppUser user) => new
    {
        user.Id,
        user.Name,
        user.UserName,
        user.Email,
        Role = user.Role.ToString(),
        UserType = user.UserType.ToString(),
        user.CompanyId,
        user.StoreGroupId,
        user.StoreId,
        user.Admin,
        user.IsActive,
        AppOperation = user.AppOperation.ToString()
    };

    private static async Task WriteSecurityEventAsync(
        ApplicationMessageLogService logs,
        ILoggerFactory loggerFactory,
        HttpContext context,
        string eventName,
        string message,
        AppUser target,
        object details,
        CancellationToken cancellationToken)
    {
        try
        {
            await logs.SuccessAsync(
                "Security",
                eventName,
                message,
                details,
                target.CompanyId,
                target.StoreGroupId,
                target.StoreId,
                CurrentUserId(context),
                context.User.Identity?.Name,
                $"access/users/{target.Id}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("SecurityAudit").LogWarning(
                ex,
                "Could not persist security event {EventName} for user {TargetUserId}.",
                eventName,
                target.Id);
        }
    }

    private static UserListItemDto ToDto(AppUser user)
    {
        return new UserListItemDto(
            user.Id,
            user.Name,
            user.UserName,
            user.Email,
            user.Role.ToString(),
            user.UserType.ToString(),
            user.CompanyId,
            user.StoreGroupId,
            user.StoreId,
            user.Admin,
            user.IsActive,
            user.AppOperation.ToString());
    }
}
