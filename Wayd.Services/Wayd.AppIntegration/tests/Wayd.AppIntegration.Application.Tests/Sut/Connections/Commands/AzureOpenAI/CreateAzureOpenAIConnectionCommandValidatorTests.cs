using FluentAssertions;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.Common.Domain.Events;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.AzureOpenAI;

public sealed class CreateAzureOpenAIConnectionCommandValidatorTests : IDisposable
{
    private const string BaseUrl = "https://ai.acme.example";
    private const string DeploymentName = "acme-deployment";

    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly CreateAzureOpenAIConnectionCommandValidator _sut;

    public CreateAzureOpenAIConnectionCommandValidatorTests()
    {
        _sut = new CreateAzureOpenAIConnectionCommandValidator(_db);
    }

    [Fact]
    public async Task Validate_Passes_WhenAnotherDeploymentSharesTheResource()
    {
        // Arrange
        AddConnection(BaseUrl, "other-deployment");
        var command = new CreateAzureOpenAIConnectionCommand("AI", null, "key", DeploymentName, BaseUrl);

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(BaseUrl, DeploymentName)]
    [InlineData("https://AI.acme.example/", "ACME-deployment")]
    public async Task Validate_Fails_WhenTheDeploymentAlreadyHasAConnection(string baseUrl, string deploymentName)
    {
        // Arrange
        AddConnection(BaseUrl, DeploymentName);
        var command = new CreateAzureOpenAIConnectionCommand("AI", null, "key", deploymentName, baseUrl);

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAzureOpenAIConnectionCommand.DeploymentName));
    }

    [Fact]
    public async Task Validate_Fails_WhenTheBaseUrlIsNotHttps()
    {
        // Arrange
        var command = new CreateAzureOpenAIConnectionCommand("AI", null, "key", DeploymentName, "http://ai.acme.example");

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAzureOpenAIConnectionCommand.BaseUrl));
    }

    public void Dispose() => _db.Dispose();

    private void AddConnection(string baseUrl, string deploymentName) =>
        _db.AddAzureOpenAIConnections([AzureOpenAIConnection.Create(
            "AI", null, new AzureOpenAIConnectionConfiguration("key", deploymentName, baseUrl), true, EventActor.System, _now)]);
}
