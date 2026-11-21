using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Infrastructure.ConnectorModules;
using Wayd.Infrastructure.Networking;
using Wayd.Integrations.Workday.Soap;

namespace Wayd.Infrastructure.Tests.Sut.ConnectorModules;

/// <summary>
/// Holds the Workday client's outbound guard and its retry classification: the registered client reaches only
/// public hosts, and a failure a retry cannot fix is not retried.
/// </summary>
public sealed class WorkdayConnectorModuleTests
{
    [Fact]
    public async Task Register_GivesTheClientAPrimaryHandlerThatRefusesNonPublicAddresses()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging();
        new WorkdayConnectorModule().Register(services);
        using var provider = services.BuildServiceProvider();
        var chain = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(WorkdayStaffingClient));

        // The primary handler is sent to directly: the resilience handlers above it would retry the refusal.
        var primary = chain;
        while (primary is DelegatingHandler delegating)
            primary = delegating.InnerHandler!;
        using var invoker = new HttpMessageInvoker(primary, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://127.0.0.1/ccx/service/acme/Staffing/v46.1");

        // Act
        var send = () => invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var thrown = (await send.Should().ThrowAsync<HttpRequestException>()).Which;
        thrown.ToString().Should().Contain("does not resolve to a public address");
        DestinationRefusedException.IsIn(thrown).Should().BeTrue("the retry policy recognises a refusal by its type");
    }

    [Fact]
    public async Task Register_DoesNotRetryARefusedDestination()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging();
        new WorkdayConnectorModule().Register(services);
        var primary = new RefusingHandler();
        services.AddHttpClient(nameof(WorkdayStaffingClient)).ConfigurePrimaryHttpMessageHandler(() => primary);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(WorkdayStaffingClient));

        // Act
        var send = () => client.PostAsync("https://workday.example/ccx/service/acme/Staffing/v46.1", null, TestContext.Current.CancellationToken);

        // Assert
        await send.Should().ThrowAsync<HttpRequestException>();
        primary.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task IsPermanentFailure_IsTrue_ForAnAuthenticationFault()
    {
        // Arrange
        using var response = Failure(new StringContent(
            "<env:Envelope xmlns:env=\"http://schemas.xmlsoap.org/soap/envelope/\"><env:Body><env:Fault><faultcode>SOAP-ENV:Client.authenticationError</faultcode><faultstring>invalid username or password</faultstring></env:Fault></env:Body></env:Envelope>",
            Encoding.UTF8, "text/xml"));

        // Act
        var result = await WorkdayConnectorModule.IsPermanentFailure(response, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPermanentFailure_IsFalse_ForATransientErrorWithoutAFault()
    {
        // Arrange
        using var response = Failure(new StringContent("Service Unavailable"), HttpStatusCode.ServiceUnavailable);

        // Act
        var result = await WorkdayConnectorModule.IsPermanentFailure(response, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPermanentFailure_IsTrue_WithoutReading_WhenTheDeclaredBodyIsAboveTheCap()
    {
        // Arrange
        var content = new StreamContent(new ThrowingStream());
        content.Headers.ContentLength = WorkdayStaffingClient.MaxResponseBytes + 1L;
        using var response = Failure(content);

        // Act
        var result = await WorkdayConnectorModule.IsPermanentFailure(response, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPermanentFailure_IsTrue_WhenAnUndeclaredBodyRunsPastTheCap()
    {
        // Arrange
        using var response = Failure(new StreamContent(new EndlessStream()));

        // Act
        var result = await WorkdayConnectorModule.IsPermanentFailure(response, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        response.RequestMessage!.Options.TryGetValue(WorkdayStaffingClient.UnreadBodyReason, out var reason).Should().BeTrue();
        reason.Should().Be(WorkdayStaffingClient.TooLargeMessage, "the client can no longer read the body to find out");
    }

    private static HttpResponseMessage Failure(HttpContent content, HttpStatusCode status = HttpStatusCode.InternalServerError) =>
        new(status) { Content = content, RequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://workday.example/") };

    /// <summary>A body with no declared length that never ends.</summary>
    private sealed class EndlessStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)'a', offset, count);
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A body that fails the test if anything reads it.</summary>
    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("The body was read.");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Refuses every request the way <see cref="PublicNetwork"/>'s handler does, counting the attempts.</summary>
    private sealed class RefusingHandler : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            throw new HttpRequestException("Connection refused.", new DestinationRefusedException("workday.example", "a public address"));
        }
    }
}
