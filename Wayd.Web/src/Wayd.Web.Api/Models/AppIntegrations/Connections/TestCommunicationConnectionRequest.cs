namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

/// <summary>
/// Optional body for <c>POST …/communication-test</c> when the connector needs a delegated Entra token.
/// </summary>
public sealed record TestCommunicationConnectionRequest
{
    /// <summary>
    /// Entra access token issued for the Teams app registration (on-behalf-of assertion).
    /// </summary>
    public string? SubjectToken { get; set; }
}
