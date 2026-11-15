using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>Connects an MCP client to a test host's <c>/mcp</c> endpoint.</summary>
public static class McpClientExtensions
{
    /// <summary>
    /// Connects over <paramref name="transport"/> once the host's <see cref="McpToolCatalog"/> is built.
    /// </summary>
    /// <remarks>
    /// A fresh host builds the catalog for several seconds after it starts, and the client's first request waits
    /// for it. The client abandons that request after five seconds, and <c>TestServer</c> answers an abandoned
    /// request with the server's eventual 500 rather than a cancellation, which the client cannot recover from.
    /// </remarks>
    public static async Task<McpClient> ConnectMcp(this WebApplicationFactory<Program> host, IClientTransport transport)
    {
        await host.Services.GetRequiredService<McpToolCatalog>().GetToolsets().WaitAsync(TestContext.Current.CancellationToken);
        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }
}
