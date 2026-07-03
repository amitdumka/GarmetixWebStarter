namespace Garmetix.Api.Auth;

public sealed class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Garmetix";
    public string ReplyToEmail { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class PasswordResetOptions
{
    public string FrontendBaseUrl { get; set; } = string.Empty;
    public string Subject { get; set; } = "Reset your Garmetix password";
}
