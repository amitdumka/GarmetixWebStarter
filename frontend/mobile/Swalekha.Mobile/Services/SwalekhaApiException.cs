namespace Swalekha.Mobile.Services;

public sealed class SwalekhaApiException : Exception
{
    public int? StatusCode { get; }

    public SwalekhaApiException(string message, int? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
