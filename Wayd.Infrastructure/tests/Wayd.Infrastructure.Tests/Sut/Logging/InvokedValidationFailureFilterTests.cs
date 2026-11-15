using Serilog;
using Serilog.Context;
using Wayd.Common.Application.Exceptions;
using Wayd.Infrastructure.Logging;

namespace Wayd.Infrastructure.Tests.Sut.Logging;

public sealed class InvokedValidationFailureFilterTests
{
    private const string InvocationFailed = "Invocation of {Message} failed!";

    private static (ILogger Logger, CollectingSink Sink) CreateLogger()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Filter.With<InvokedValidationFailureFilter>()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    [Fact]
    public void IsEnabled_DropsAnInvokedValidationFailureInsideARequest()
    {
        // Arrange
        var (logger, sink) = CreateLogger();

        // Act
        using (LogContext.PushProperty("RequestId", "0HN:00000001"))
            logger.Error(new ValidationException(), InvocationFailed, "GetRowsQuery");

        // Assert
        sink.Events.Should().BeEmpty();
    }

    [Fact]
    public void IsEnabled_KeepsAValidationFailureOutsideARequest()
    {
        // Arrange — a message handled off a queue has no other record of its failure
        var (logger, sink) = CreateLogger();

        // Act
        logger.Error(new ValidationException(), InvocationFailed, "ImportRowCommand");

        // Assert
        sink.Events.Should().ContainSingle();
    }

    [Fact]
    public void IsEnabled_KeepsAnyOtherFailedInvocation()
    {
        // Arrange
        var (logger, sink) = CreateLogger();

        // Act
        using (LogContext.PushProperty("RequestId", "0HN:00000001"))
            logger.Error(new InvalidOperationException("boom"), InvocationFailed, "GetRowsQuery");

        // Assert
        sink.Events.Should().ContainSingle();
    }
}
