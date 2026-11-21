namespace Wayd.Integrations.AzureDevOps.Clients;

/// <summary>
/// Azure DevOps answered a request with an error, or with a response that cannot be used. The message
/// carries Azure DevOps's reply, such as its status and error text, which an admin needs to put the
/// connection right, so it may be recorded in sync history, where other exception text may not.
/// </summary>
public sealed class AzureDevOpsResponseException(string message) : Exception(message);
