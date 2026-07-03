using Garmetix.Core.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Authentication;

public class PasswordResetToken : IEntity
{
    public PasswordResetToken()
    {
        Id = Guid.NewGuid();
        TokenHash = string.Empty;
        RequestIpAddress = string.Empty;
        RequestUserAgent = string.Empty;
    }

    [Display(Name = "Id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Display(Name = "User Id")] public Guid UserId { get; set; }
    [Display(Name = "Token Hash")] public string TokenHash { get; set; }
    [Display(Name = "Created At UTC")] public DateTime CreatedAtUtc { get; set; }
    [Display(Name = "Expires At UTC")] public DateTime ExpiresAtUtc { get; set; }
    [Display(Name = "Used At UTC")] public DateTime? UsedAtUtc { get; set; }
    [Display(Name = "Revoked At UTC")] public DateTime? RevokedAtUtc { get; set; }
    [Display(Name = "Request IP Address")] public string RequestIpAddress { get; set; }
    [Display(Name = "Request User Agent")] public string RequestUserAgent { get; set; }
}
