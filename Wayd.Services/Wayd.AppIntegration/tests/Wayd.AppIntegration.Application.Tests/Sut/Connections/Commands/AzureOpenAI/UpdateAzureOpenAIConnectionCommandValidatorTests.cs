using FluentAssertions;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.Common.Domain.Events;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.AzureOpenAI;

public sealed class UpdateAzureOpenAIConnectionCommandValidatorTests : IDisposable
{
    private const string BaseUrl = "https://ai.acme.example";
    private const string DeploymentName = "acme-deployment";

    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly UpdateAzureOpenAIConnectionCommandValidator _sut;

    public UpdateAzureOpenAIConnectionCommandValidatorTests()
    {
        _sut = new UpdateAzureOpenAIConnectionCommandValidator(_db);
    }

    [Fact]
    public async Task Validate_Passes_WhenTheConnectionKeepsItsOwnUrlAndDeployment()
    {
        // Arrange
        var connection = AddConnection(BaseUrl, DeploymentName);
        var command = new UpdateAzureOpenAIConnectionCommand(connection.Id, "Renamed", BaseUrl, null, DeploymentName, null);

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(BaseUrl, DeploymentName)]
    [InlineData("https://AI.acme.example/", "ACME-deployment")]
    public async Task Validate_Fails_WhenAnotherConnectionHasTheSameUrlAndDeployment(string baseUrl, string deploymentName)
    {
        // Arrange
        var connection = AddConnection(BaseUrl, "other-deployment");
        AddConnection(BaseUrl, DeploymentName);
        var command = new UpdateAzureOpenAIConnectionCommand(connection.Id, "AI", baseUrl, null, deploymentName, null);

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://ai.acme.example")]
    [InlineData("ai.acme.example")]
    public async Task Validate_Fails_WhenTheBaseUrlIsNotAbsoluteHttps(string baseUrl)
    {
        // Arrange
        var connection = AddConnection(BaseUrl, DeploymentName);
        var command = new UpdateAzureOpenAIConnectionCommand(connection.Id, "AI", baseUrl, null, DeploymentName, "new-key");

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateAzureOpenAIConnectionCommand.BaseUrl));
    }

    public void Dispose() => _db.Dispose();

    private AzureOpenAIConnection AddConnection(string baseUrl, string deploymentName)
    {
        var connection = AzureOpenAIConnection.Create(
            "AI", null, new AzureOpenAIConnectionConfiguration("key", deploymentName, baseUrl), true, EventActor.System, _now);
        _db.AddAzureOpenAIConnections([connection]);
        return connection;
    }
}
