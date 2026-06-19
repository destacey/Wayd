using System.Net;

namespace Wayd.Common.Application.Exceptions;

/// <summary>
/// Thrown when a feature exists on the server but is not enabled or configured
/// on this deployment. Maps to HTTP 503 Service Unavailable. Example: the Entra
/// token-exchange endpoint on a local-only deployment.
/// </summary>
public class ServiceUnavailableException : CustomException
{
    public ServiceUnavailableException(string message)
        : base(message, null, HttpStatusCode.ServiceUnavailable)
    {
    }
}
