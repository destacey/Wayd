using FluentAssertions;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.Common.Domain.Events;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Domain.Tests.Sut.Models.AzureOpenAI;

/// <summary>
/// Holds the rules for the resource URL an edit may set: it must be absolute HTTPS, and the stored API key is
/// only sent to the host it was entered for.
/// </summary>
public sealed class AzureOpenAIConnectionTests
{
    private const string OriginalUrl = "https://acme.openai.azure.com";
    private const string OriginalKey = "original-key";

    private static readonly EventActor Actor = EventActor.User(Guid.NewGuid().ToString());

    private readonly TestingDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 02, 10, 12, 0, 0));

    [Fact]
    public void Update_RefusesToKeepTheKey_WhenTheHostChanges()
    {
        // Arrange
        var connection = Create();
        var eventsBefore = connection.DomainEvents.Count;

        // Act
        var result = connection.Update("Renamed", null, "https://attacker.example.com", null, "gpt-5", true, Actor, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Connection.CredentialsRequiredMessage("API key"));
        connection.Name.Should().Be("AI");
        connection.Configuration.BaseUrl.Should().Be(OriginalUrl);
        connection.Configuration.DeploymentName.Should().Be("gpt-4o");
        connection.DomainEvents.Should().HaveCount(eventsBefore);
    }

    [Fact]
    public void Update_ChangesTheHost_WhenTheKeyIsEnteredAgain()
    {
        // Arrange
        var connection = Create();

        // Act
        var result = connection.Update("AI", null, "https://acme-east.openai.azure.com", "new-key", "gpt-4o", true, Actor, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.BaseUrl.Should().Be("https://acme-east.openai.azure.com");
        connection.Configuration.ApiKey.Should().Be("new-key");
    }

    [Fact]
    public void Update_KeepsTheKey_WhenOnlyThePathOrDeploymentChanges()
    {
        // Arrange
        var connection = Create();

        // Act
        var result = connection.Update("AI", null, "https://acme.openai.azure.com/openai/v1", null, "gpt-5", true, Actor, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.BaseUrl.Should().Be("https://acme.openai.azure.com/openai/v1");
        connection.Configuration.DeploymentName.Should().Be("gpt-5");
        connection.Configuration.ApiKey.Should().Be(OriginalKey);
    }

    [Theory]
    [InlineData("http://acme.openai.azure.com")]
    [InlineData("acme.openai.azure.com")]
    [InlineData("https://user:pass@acme.openai.azure.com")]
    public void Update_RefusesABaseUrlThatIsNotAbsoluteHttps(string baseUrl)
    {
        // Arrange
        var connection = Create();
        var eventsBefore = connection.DomainEvents.Count;

        // Act
        var result = connection.Update("Renamed", null, baseUrl, "new-key", "gpt-4o", true, Actor, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        connection.Name.Should().Be("AI");
        connection.Configuration.BaseUrl.Should().Be(OriginalUrl);
        connection.Configuration.ApiKey.Should().Be(OriginalKey);
        connection.DomainEvents.Should().HaveCount(eventsBefore);
    }

    [Theory]
    [InlineData("http://acme.openai.azure.com")]
    [InlineData("acme.openai.azure.com")]
    public void Create_RefusesABaseUrlThatIsNotAbsoluteHttps(string baseUrl)
    {
        // Arrange
        var configuration = new AzureOpenAIConnectionConfiguration(OriginalKey, "gpt-4o", baseUrl);

        // Act
        var act = () => AzureOpenAIConnection.Create("AI", null, configuration, true, Actor, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private AzureOpenAIConnection Create() =>
        AzureOpenAIConnection.Create(
            "AI",
            null,
            new AzureOpenAIConnectionConfiguration(OriginalKey, "gpt-4o", OriginalUrl),
            true,
            Actor,
            _dateTimeProvider.Now);
}
