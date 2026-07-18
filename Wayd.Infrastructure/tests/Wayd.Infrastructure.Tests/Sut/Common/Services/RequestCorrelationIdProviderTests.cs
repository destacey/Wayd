using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Wayd.Infrastructure.Common.Services;

namespace Wayd.Infrastructure.Tests.Sut.Common.Services;

public sealed class RequestCorrelationIdProviderTests
{
    [Fact]
    public void CorrelationId_WhenHttpContextPresent_ReturnsTraceIdentifier()
    {
        // Arrange
        var httpContext = new DefaultHttpContext { TraceIdentifier = "http-trace-id" };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(httpContext);
        var sut = new RequestCorrelationIdProvider(accessor.Object);

        // Act
        var result = sut.CorrelationId;

        // Assert
        result.Should().Be("http-trace-id");
    }

    [Fact]
    public void CorrelationId_WhenNoHttpContextButActiveActivity_ReturnsActivityTraceId()
    {
        // Arrange
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = new RequestCorrelationIdProvider(accessor.Object);

        using var listener = CreateAlwaysOnListener("correlation-tests");
        using var source = new ActivitySource("correlation-tests");
        using var activity = source.StartActivity("job");
        activity.Should().NotBeNull("an activity must be recording for this scenario to be meaningful");

        // Act
        var result = sut.CorrelationId;

        // Assert
        result.Should().Be(activity!.TraceId.ToString());
    }

    [Fact]
    public void CorrelationId_WhenNoHttpContextAndNoActivity_ReturnsParsableGuid()
    {
        // Arrange
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = new RequestCorrelationIdProvider(accessor.Object);

        Activity.Current = null;

        // Act
        var result = sut.CorrelationId;

        // Assert
        Guid.TryParse(result, out _).Should().BeTrue("the last-resort correlation id is a new Guid");
    }

    private static ActivityListener CreateAlwaysOnListener(string sourceName)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
