namespace Wayd.Infrastructure.OpenTelemetry;

/// <summary>
/// Names for the traces and metrics the hosted MCP endpoint records for each tool call, so its load can be told apart
/// from the same endpoints called directly. Tool calls run through the API in process, without the hosting layer, so
/// ASP.NET Core's own request telemetry never sees them.
/// </summary>
public static class McpTelemetry
{
    /// <summary>The name of both the activity source and the meter.</summary>
    public const string Name = "Wayd.Mcp";

    /// <summary>The histogram of tool call durations, in seconds.</summary>
    public const string ToolCallDuration = "wayd.mcp.tool.duration";

    /// <summary>The tag naming the tool called.</summary>
    public const string ToolTag = "wayd.mcp.tool";

    /// <summary>The tag naming how the caller authenticated: <c>oauth</c>, <c>personal_access_token</c> or <c>session</c>.</summary>
    public const string CredentialTag = "wayd.mcp.credential";

    /// <summary>
    /// The tag naming the OAuth client acting for the user. On traces only: anyone can be a client, so as a metric
    /// dimension it would have no bound.
    /// </summary>
    public const string ClientIdTag = "wayd.mcp.client_id";
}
