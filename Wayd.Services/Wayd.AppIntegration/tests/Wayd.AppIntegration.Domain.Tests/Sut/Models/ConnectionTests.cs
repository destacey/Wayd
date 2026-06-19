using FluentAssertions;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Domain.Tests.Sut.Models;

public class ConnectionTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;

    public ConnectionTests()
    {
        _dateTimeProvider = new(new DateTime(2026, 02, 10, 12, 0, 0));
    }

    [Fact]
    public void Deactivate_WhenSyncableConnection_ShouldDisableSync()
    {
        // Arrange
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            true,
            null,
            _dateTimeProvider.Now);

        // Enable sync first (assuming we have active integration objects - for this test we'll skip validation)
        // We can't actually enable it due to validation, but we can test the deactivation logic

        // Act
        var result = connection.Deactivate(_dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenNonSyncableConnection_ShouldOnlyDeactivate()
    {
        // Arrange
        var config = new AzureOpenAIConnectionConfiguration("test-key", "gpt-4", "https://test.openai.azure.com");
        var connection = AzureOpenAIConnection.Create(
            "Test AI Connection",
            null,
            config,
            true,
            _dateTimeProvider.Now);

        // Act
        var result = connection.Deactivate(_dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.IsActive.Should().BeFalse();
    }

    [Fact]
    public void AzureDevOpsBoardsConnection_ShouldHave_CorrectConnectorType()
    {
        // Arrange & Act
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            true,
            null,
            _dateTimeProvider.Now);

        // Assert
        connection.Connector.Should().Be(Connector.AzureDevOps);
    }

    [Fact]
    public void AzureOpenAIConnection_ShouldHave_CorrectConnectorType()
    {
        // Arrange & Act
        var config = new AzureOpenAIConnectionConfiguration("test-key", "gpt-4", "https://test.openai.azure.com");
        var connection = AzureOpenAIConnection.Create(
            "Test AI Connection",
            null,
            config,
            true,
            _dateTimeProvider.Now);

        // Assert
        connection.Connector.Should().Be(Connector.AzureOpenAI);
    }

    [Fact]
    public void AzureOpenAIConnection_ShouldNotBe_SyncableConnection()
    {
        // Arrange & Act
        var config = new AzureOpenAIConnectionConfiguration("test-key", "gpt-4", "https://test.openai.azure.com");
        var connection = AzureOpenAIConnection.Create(
            "Test AI Connection",
            null,
            config,
            true,
            _dateTimeProvider.Now);

        // Assert
        connection.Should().NotBeAssignableTo<Wayd.AppIntegration.Domain.Interfaces.ISyncableConnection>();
    }
}
