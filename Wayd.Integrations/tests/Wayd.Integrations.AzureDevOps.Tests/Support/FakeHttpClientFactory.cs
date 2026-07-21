namespace Wayd.Integrations.AzureDevOps.Tests.Support;

/// <summary>
/// Test double for <see cref="IHttpClientFactory"/> that hands out a new <see cref="HttpClient"/>
/// per call — mirroring the real factory — all wired to the same <see cref="StubHttpMessageHandler"/>
/// so tests can assert on every request the service under test issues, however many clients it creates.
/// </summary>
public sealed class FakeHttpClientFactory(StubHttpMessageHandler handler) : IHttpClientFactory
{
    public int CreateClientCallCount { get; private set; }

    public HttpClient CreateClient(string name)
    {
        CreateClientCallCount++;
        // disposeHandler: false — the shared handler must outlive any one HttpClient, same as the
        // real factory's pooled handler behind named clients.
        return new HttpClient(handler, disposeHandler: false);
    }
}
