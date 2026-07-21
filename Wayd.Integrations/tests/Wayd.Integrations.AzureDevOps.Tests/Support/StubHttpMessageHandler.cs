using System.Net;
using System.Text;

namespace Wayd.Integrations.AzureDevOps.Tests.Support;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that records outgoing requests (with buffered
/// bodies) and replays enqueued JSON responses in order. Thread-safe so tests can exercise code
/// paths that issue concurrent requests (e.g. bounded-parallel team-settings lookups).
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Lock _lock = new();
    private readonly Queue<(HttpStatusCode StatusCode, string Json)> _responses = new();

    public List<CapturedRequest> Requests { get; } = [];

    /// <summary>When true, the next SendAsync call throws OperationCanceledException instead of
    /// returning a response — simulates the caller's token firing mid-request.</summary>
    public bool ThrowOperationCanceledOnNextSend { get; set; }

    public void EnqueueResponse(HttpStatusCode statusCode, string json)
    {
        lock (_lock)
        {
            _responses.Enqueue((statusCode, json));
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (ThrowOperationCanceledOnNextSend)
        {
            ThrowOperationCanceledOnNextSend = false;
            throw new OperationCanceledException("Simulated cancellation mid-request.");
        }

        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        HttpStatusCode statusCode;
        string json;
        lock (_lock)
        {
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri, body));

            if (_responses.Count == 0)
                throw new InvalidOperationException($"No stub response enqueued for request {request.Method} {request.RequestUri}.");

            (statusCode, json) = _responses.Dequeue();
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    public sealed record CapturedRequest(HttpMethod Method, Uri? Uri, string? Body);
}
