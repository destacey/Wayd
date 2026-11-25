using System.Net;

namespace Wayd.Common.Application.Exceptions;

public class CustomException : Exception
{
    public List<string>? ErrorMessages { get; }

    public HttpStatusCode StatusCode { get; }

    public CustomException(string message, List<string>? errors = default, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        : base(message)
    {
        ErrorMessages = errors;
        StatusCode = statusCode;
    }

    /// <summary>
    /// An outcome caused by <paramref name="innerException"/>, kept so the log shows where it came from. The
    /// status comes before the cause so a call passing <see langword="null"/> errors still binds to the
    /// constructor above.
    /// </summary>
    protected CustomException(string message, HttpStatusCode statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}