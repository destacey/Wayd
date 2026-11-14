using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

/// <summary>
/// A tool call is a request the API serves in process, with no server underneath to end it, so what a server does when
/// a request finishes has to happen here too.
/// </summary>
public sealed class ApiPipelineTests
{
    [Fact]
    public async Task Send_DisposesTheRequestsScopedServices_WhenTheCallEnds()
    {
        // Arrange
        Probe? resolved = null;
        await using var services = new ServiceCollection()
            .AddOptions()
            .AddScoped<Probe>()
            .AddSingleton<IHttpContextFactory, DefaultHttpContextFactory>()
            .BuildServiceProvider();
        var pipeline = Build(services, context =>
        {
            resolved = context.RequestServices.GetRequiredService<Probe>();
            return Task.CompletedTask;
        });

        // Act
        await pipeline.Send("Test_Tool", new DefaultHttpContext(), HttpMethods.Get, "/api/test", QueryString.Empty, null, null, TestContext.Current.CancellationToken);

        // Assert
        resolved.Should().NotBeNull();
        resolved!.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Send_RunsTheRequestsCompletionCallbacks_WhenTheCallEnds()
    {
        // Arrange
        var completed = false;
        await using var services = new ServiceCollection()
            .AddOptions()
            .AddSingleton<IHttpContextFactory, DefaultHttpContextFactory>()
            .BuildServiceProvider();
        var pipeline = Build(services, context =>
        {
            context.Response.OnCompleted(() =>
            {
                completed = true;
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        });

        // Act
        await pipeline.Send("Test_Tool", new DefaultHttpContext(), HttpMethods.Get, "/api/test", QueryString.Empty, null, null, TestContext.Current.CancellationToken);

        // Assert
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task Send_AnswersTheResponseAndStillDisposesTheScope_WhenACompletionCallbackFails()
    {
        // Arrange — the response is decided by then, so a failure there must not turn it into an error
        Probe? resolved = null;
        await using var services = new ServiceCollection()
            .AddOptions()
            .AddScoped<Probe>()
            .AddSingleton<IHttpContextFactory, DefaultHttpContextFactory>()
            .BuildServiceProvider();
        var pipeline = Build(services, context =>
        {
            resolved = context.RequestServices.GetRequiredService<Probe>();
            context.Response.OnCompleted(() => throw new InvalidOperationException("Callback failed."));
            context.Response.StatusCode = StatusCodes.Status201Created;
            return Task.CompletedTask;
        });

        // Act
        var response = await pipeline.Send("Test_Tool", new DefaultHttpContext(), HttpMethods.Post, "/api/test", QueryString.Empty, null, null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(StatusCodes.Status201Created);
        resolved!.Disposed.Should().BeTrue();
    }

    /// <summary>An <see cref="ApiPipeline"/> whose API is <paramref name="endpoint"/>.</summary>
    private static ApiPipeline Build(IServiceProvider services, RequestDelegate endpoint)
    {
        var pipeline = new ApiPipeline(services.GetRequiredService<IHttpContextFactory>(), NullLogger<ApiPipeline>.Instance);
        var app = new ApplicationBuilder(services);
        pipeline.Configure(a => a.Run(endpoint))(app);
        app.Build();
        return pipeline;
    }

    /// <summary>A scoped service that records whether its scope disposed it.</summary>
    private sealed class Probe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
