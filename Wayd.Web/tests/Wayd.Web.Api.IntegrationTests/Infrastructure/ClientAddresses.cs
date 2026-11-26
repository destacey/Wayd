using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Gives a test's requests a client address of their own, so the limits keyed by caller address start empty for it on a
/// host other tests share.
/// </summary>
/// <remarks>
/// Every request through the test server otherwise arrives from the same address, so one test spending the sign-in or
/// OAuth limit would throttle every test after it on that host. <see cref="StartupFilter"/> takes the address from
/// <see cref="Header"/>, ahead of the whole pipeline the rate limiter runs in; it is registered only by the test factory.
/// </remarks>
internal static class ClientAddresses
{
    /// <summary>The request header carrying the address the test server records as the connection's.</summary>
    public const string Header = "X-Test-Client-Address";

    private static int _next;

    /// <summary>
    /// A client of <paramref name="host"/> whose requests come from an address no other client in the process uses. Only a
    /// host derived from <see cref="WaydSqlServerApiFactory"/> reads the address; on any other the header is ignored.
    /// </summary>
    /// <param name="host">The host to send requests to.</param>
    /// <param name="options">The client's options, as <see cref="WebApplicationFactory{TEntryPoint}.CreateClient(WebApplicationFactoryClientOptions)"/> takes them.</param>
    public static HttpClient CreateClientFromNewAddress(this WebApplicationFactory<Program> host, WebApplicationFactoryClientOptions? options = null)
    {
        var client = host.CreateClient(options ?? new WebApplicationFactoryClientOptions());
        client.DefaultRequestHeaders.Add(Header, Next().ToString());
        return client;
    }

    // Counted through 10.0.0.0/8, which is private and never a real caller here; each one is an address in full, so
    // ClientAddressKey keys each separately.
    private static IPAddress Next()
    {
        var n = Interlocked.Increment(ref _next);
        return new IPAddress([10, (byte)(n >> 16), (byte)(n >> 8), (byte)n]);
    }

    /// <summary>Sets the connection's remote address from <see cref="Header"/>, before anything else in the pipeline reads it.</summary>
    public sealed class StartupFilter : IStartupFilter
    {
        /// <inheritdoc/>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(Header, out var value) && IPAddress.TryParse(value, out var address))
                    context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
