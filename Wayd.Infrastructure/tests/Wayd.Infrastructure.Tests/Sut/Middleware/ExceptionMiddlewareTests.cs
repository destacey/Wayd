using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain;
using Wayd.Infrastructure.Middleware;
using MiddlewareServices = Wayd.Infrastructure.Middleware.ConfigureServices;

namespace Wayd.Infrastructure.Tests.Sut.Middleware;

public sealed class ExceptionMiddlewareTests
{
    private static (ExceptionMiddleware Middleware, DefaultHttpContext Context) Create(string environmentName = "Production")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = MiddlewareServices.IdentifyProblem);
        var provider = services.BuildServiceProvider();

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("user-1");

        var middleware = new ExceptionMiddleware(
            provider.GetRequiredService<IProblemDetailsService>(), currentUser.Object, environment.Object);

        var context = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "0HN:00000001" };
        context.Request.Method = "GET";
        context.Request.Path = "/api/things";
        context.Response.Body = new MemoryStream();

        return (middleware, context);
    }

    private static RequestDelegate Throws(Exception exception) => _ => throw exception;

    private static JsonElement ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body).RootElement;
    }

    [Fact]
    public async Task InvokeAsync_HidesAnUnhandledExceptionsMessageOutsideDevelopment()
    {
        // Arrange
        var (middleware, context) = Create();
        var exception = new InvalidOperationException("wrapper", new Exception("Cannot insert duplicate key in object 'Work.WorkItems'."));

        // Act
        await middleware.InvokeAsync(context, Throws(exception));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var body = ReadBody(context);
        body.GetProperty("detail").GetString().Should().Be(ErrorMessages.Unexpected);
        body.GetRawText().Should().NotContain("duplicate key");
        body.GetProperty("requestId").GetString().Should().Be("0HN:00000001");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ReturnsTheInnermostMessageInDevelopment()
    {
        // Arrange
        var (middleware, context) = Create(Environments.Development);
        var exception = new InvalidOperationException("wrapper", new Exception("the real cause"));

        // Act
        await middleware.InvokeAsync(context, Throws(exception));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        ReadBody(context).GetProperty("detail").GetString().Should().Be("the real cause");
    }

    [Fact]
    public async Task InvokeAsync_KeepsTheMessageOfACustomException()
    {
        // Arrange
        var (middleware, context) = Create();

        // Act
        await middleware.InvokeAsync(context, Throws(new NotFoundException("Project 42 was not found.")));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ReadBody(context).GetProperty("detail").GetString().Should().Be("Project 42 was not found.");
    }

    [Fact]
    public async Task InvokeAsync_AnswersAWrappedCustomExceptionWithItsOwnStatusAndMessage()
    {
        // Arrange
        var (middleware, context) = Create();
        var exception = new InvalidOperationException("wrapper", new NotFoundException("Project 42 was not found."));

        // Act
        await middleware.InvokeAsync(context, Throws(exception));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ReadBody(context).GetProperty("detail").GetString().Should().Be("Project 42 was not found.");
    }

    [Fact]
    public async Task InvokeAsync_AnswersAWrappedValidationExceptionWith422()
    {
        // Arrange
        var (middleware, context) = Create();
        var validation = new ValidationException([new FluentValidation.Results.ValidationFailure("Name", "Name is required.")]);

        // Act
        await middleware.InvokeAsync(context, Throws(new AggregateException(validation)));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        ReadBody(context).GetProperty("errors").GetProperty("Name")[0].GetString().Should().Be("Name is required.");
    }

    [Fact]
    public async Task InvokeAsync_AnswersAValidationExceptionWithValidationProblemDetails()
    {
        // Arrange
        var (middleware, context) = Create();
        var exception = new ValidationException([new FluentValidation.Results.ValidationFailure("Name", "Name is required.")]);

        // Act
        await middleware.InvokeAsync(context, Throws(exception));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var body = ReadBody(context);
        body.GetProperty("errors").GetProperty("Name")[0].GetString().Should().Be("Name is required.");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    [InlineData(StatusCodes.Status400BadRequest)]
    public async Task InvokeAsync_AnswersABadRequestWithItsStatusCode(int statusCode)
    {
        // Arrange
        var (middleware, context) = Create();

        // Act
        await middleware.InvokeAsync(context, Throws(new BadHttpRequestException("Request body too large.", statusCode)));

        // Assert
        context.Response.StatusCode.Should().Be(statusCode);
        ReadBody(context).GetProperty("detail").GetString().Should().Be("Request body too large.");
    }

    [Fact]
    public async Task InvokeAsync_WritesNothingForABadRequestOnceTheResponseHasStarted()
    {
        // Arrange
        var (middleware, context) = Create();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context, Throws(new BadHttpRequestException("Unexpected end of request content.")));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Body.Length.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_WritesNothingWhenTheClientAbortedTheRequest()
    {
        // Arrange
        var (middleware, context) = Create();
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        context.RequestAborted = aborted.Token;

        // Act
        await middleware.InvokeAsync(context, Throws(new OperationCanceledException()));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Body.Length.Should().Be(0);
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
