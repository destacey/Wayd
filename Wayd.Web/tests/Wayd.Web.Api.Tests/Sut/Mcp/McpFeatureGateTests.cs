using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Moq;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.Hosting;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.Tests.Sut.Mcp;

/// <summary>
/// The gate decides whether the MCP endpoint and its authorization server answer at all, so a gap in it publishes
/// endpoints that should be hidden or issues tokens for a host the caller chose.
/// </summary>
public sealed class McpFeatureGateTests
{
    private const string Configured = "https://api.wayd.example.com";

    [Theory]
    [InlineData("/mcp")]
    [InlineData("/oauth/authorize")]
    [InlineData("/oauth/token")]
    [InlineData("/.well-known/oauth-authorization-server")]
    [InlineData("/.well-known/oauth-protected-resource/mcp")]
    public async Task Gate_RefusesOutsideDevelopment_WhileNoPublicUrlIsConfigured(string path)
    {
        // Arrange
        var app = Pipeline(Environments.Production, publicUrl: null, flagOn: true);

        // Act
        var status = await Send(app, path);

        // Assert
        status.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Theory]
    [InlineData("Production", Configured)]
    [InlineData("Development", null)]
    public async Task Gate_LetsTheRequestThrough_WithAPublicUrlOrInDevelopment(string environment, string? publicUrl)
    {
        // Arrange
        var app = Pipeline(environment, publicUrl, flagOn: true);

        // Act
        var status = await Send(app, "/mcp");

        // Assert
        status.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Gate_HidesTheEndpoint_WhileTheFlagIsOff_WhateverThePublicUrl()
    {
        // Arrange
        var app = Pipeline(Environments.Production, publicUrl: null, flagOn: false);

        // Act
        var status = await Send(app, "/mcp");

        // Assert
        status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Gate_LeavesTheRestOfTheApiAlone()
    {
        // Arrange
        var app = Pipeline(Environments.Production, publicUrl: null, flagOn: false);

        // Act
        var status = await Send(app, "/api/projects");

        // Assert
        status.Should().Be(StatusCodes.Status200OK);
    }

    /// <summary>The gate in front of an endpoint that answers 200 to everything.</summary>
    private static RequestDelegate Pipeline(string environment, string? publicUrl, bool flagOn)
    {
        var features = new Mock<IFeatureManager>();
        features.Setup(f => f.IsEnabledAsync(FeatureFlags.Names.McpServer)).ReturnsAsync(flagOn);
        var services = new ServiceCollection().AddSingleton(features.Object).BuildServiceProvider();
        var host = new Mock<IHostEnvironment>();
        host.SetupGet(h => h.EnvironmentName).Returns(environment);

        var app = new ApplicationBuilder(services);
        new McpFeatureGate(host.Object, new PublicUrl(publicUrl)).Configure(a => a.Run(c =>
        {
            c.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }))(app);
        var pipeline = app.Build();
        return context =>
        {
            context.RequestServices = services;
            return pipeline(context);
        };
    }

    /// <summary>Sends a GET to <paramref name="path"/> and answers the status code.</summary>
    private static async Task<int> Send(RequestDelegate app, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        await app(context);
        return context.Response.StatusCode;
    }
}
