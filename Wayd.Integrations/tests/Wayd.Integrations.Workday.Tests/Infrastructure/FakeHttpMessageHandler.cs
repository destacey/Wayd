using System.Net;
using System.Text;

namespace Wayd.Integrations.Workday.Tests.Infrastructure;

/// <summary>
/// Minimal HttpMessageHandler that returns canned responses. Used to drive
/// <c>WorkdayStaffingClient</c> in tests without hitting a live tenant.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Captured { get; } = [];

    public Queue<HttpResponseMessage> Responses { get; } = new();

    public void EnqueueXml(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/xml"),
        };
        Responses.Enqueue(response);
    }

    public void EnqueueFault(string body, HttpStatusCode status = HttpStatusCode.InternalServerError)
        => EnqueueXml(body, status);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Captured.Add(await CloneAsync(request, cancellationToken));

        if (Responses.Count == 0)
            throw new InvalidOperationException("No fake responses configured.");

        return Responses.Dequeue();
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            clone.Content = new StringContent(body, Encoding.UTF8, "text/xml");
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }
}
