using Wayd.AppIntegration.Application.Connections.Commands;
using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;
using Wayd.AppIntegration.Application.Connections.Commands.Entra;
using Wayd.AppIntegration.Application.Connections.Commands.Workday;

namespace Wayd.AppIntegration.Application.Logging;

/// <summary>
/// Produces a loggable/error-string-safe representation of a connection Create/Update command by
/// blanking its one secret property (PAT, password, client secret, API key). These commands are
/// otherwise logged/echoed whole (via structured "{@Request}" destructuring or plain string
/// interpolation, both of which call ToString()/serialize every property) whenever the handler's
/// catch block fires — this exists so that path never puts the raw secret in a log sink or in a
/// Result.Failure string that can flow back to the API/UI.
/// </summary>
public static class RequestRedaction
{
    private const string Redacted = "[redacted]";

    public static object Redact(this CreateAzureDevOpsConnectionCommand request) =>
        request with { PersonalAccessToken = Redacted };

    public static object Redact(this UpdateAzureDevOpsConnectionCommand request) =>
        request with { PersonalAccessToken = Redacted };

    public static object Redact(this CreateWorkdayConnectionCommand request) =>
        request with { IsuPassword = Redacted };

    public static object Redact(this UpdateWorkdayConnectionCommand request) =>
        request with { IsuPassword = Redacted };

    public static object Redact(this CreateEntraConnectionCommand request) =>
        request with { ClientSecret = Redacted };

    public static object Redact(this UpdateEntraConnectionCommand request) =>
        request with { ClientSecret = Redacted };

    public static object Redact(this CreateAzureOpenAIConnectionCommand request) =>
        request with { ApiKey = Redacted };

    public static object Redact(this UpdateAzureOpenAIConnectionCommand request) =>
        request with { ApiKey = Redacted };
}
