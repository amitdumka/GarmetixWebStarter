using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Garmetix.Core.Models.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace Garmetix.Api.Auth;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public AuthResponse CreateToken(AppUser user)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "Garmetix";
        var audience = configuration["Jwt:Audience"] ?? "GarmetixWeb";
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
        var expiresAtUtc = DateTime.UtcNow.AddHours(10);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("userType", user.UserType.ToString()),
            new("admin", user.Admin.ToString())
        };

        AddOptionalClaim(claims, "companyId", user.CompanyId);
        AddOptionalClaim(claims, "storeGroupId", user.StoreGroupId);
        AddOptionalClaim(claims, "storeId", user.StoreId);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc,
            ToDto(user));
    }

    public static AuthUserDto ToDto(AppUser user)
    {
        return new AuthUserDto(
            user.Id,
            user.Name,
            user.UserName,
            user.Email,
            user.Role.ToString(),
            user.UserType.ToString(),
            user.CompanyId,
            user.StoreGroupId,
            user.StoreId,
            user.Admin);
    }

    private static void AddOptionalClaim(List<Claim> claims, string name, Guid? value)
    {
        if (value is { } guid)
        {
            claims.Add(new Claim(name, guid.ToString()));
        }
    }
}
