using System.Security.Claims;
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
        group.MapDelete("/{id:guid}", DeleteUserAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IReadOnlyList<UserListItemDto>> ListUsersAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        return await db.Users
            .AsNoTracking()
            .OrderBy(user => user.Name)
            .Select(user => ToDto(user))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> CreateUserAsync(SaveUserRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
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
            Admin = request.Admin || request.Role == LoginRole.Admin,
            AppOperation = request.AppOperation
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/access/users/{user.Id}", ToDto(user));
    }

    private static async Task<IResult> UpdateUserAsync(Guid id, SaveUserRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var validation = ValidateUserRequest(request, requirePassword: false);
        if (validation is not null)
        {
            return validation;
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

        var willRemoveAdmin = (user.Admin || user.Role == LoginRole.Admin) && !request.Admin && request.Role != LoginRole.Admin;
        if (willRemoveAdmin && await IsLastAdminAsync(user.Id, db, cancellationToken))
        {
            return Results.Conflict(new { message = "Cannot remove admin access from the last admin user." });
        }

        user.Name = request.Name.Trim();
        user.UserName = normalizedUserName;
        user.Email = normalizedEmail;
        user.Role = request.Role;
        user.UserType = request.UserType;
        user.CompanyId = request.CompanyId;
        user.StoreGroupId = request.StoreGroupId;
        user.StoreId = request.StoreId;
        user.Admin = request.Admin || request.Role == LoginRole.Admin;
        user.AppOperation = request.AppOperation;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.Password = PasswordHasher.Hash(request.Password);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDto(user));
    }

    private static async Task<IResult> DeleteUserAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var currentUserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(currentUserId, out var currentId) && currentId == id)
        {
            return Results.Conflict(new { message = "You cannot delete your own signed-in user." });
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        if ((user.Admin || user.Role == LoginRole.Admin) && await IsLastAdminAsync(user.Id, db, cancellationToken))
        {
            return Results.Conflict(new { message = "Cannot delete the last admin user." });
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static IResult? ValidateUserRequest(SaveUserRequest request, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Email))
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

    private static async Task<bool> IsLastAdminAsync(Guid userId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var adminCount = await db.Users.CountAsync(user => user.Admin || user.Role == LoginRole.Admin, cancellationToken);
        var userIsAdmin = await db.Users.AnyAsync(user => user.Id == userId && (user.Admin || user.Role == LoginRole.Admin), cancellationToken);

        return userIsAdmin && adminCount <= 1;
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
            user.AppOperation.ToString());
    }
}
