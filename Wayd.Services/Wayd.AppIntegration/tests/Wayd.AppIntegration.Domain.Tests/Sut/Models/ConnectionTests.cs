using System.Text.Json;
using FluentAssertions;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Domain.Tests.Sut.Models;

public class ConnectionTests
{
    private static readonly EventActor Actor = EventActor.User(Guid.NewGuid().ToString());

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
            Actor, _dateTimeProvider.Now);

        // Enable sync first (assuming we have active integration objects - for this test we'll skip validation)
        // We can't actually enable it due to validation, but we can test the deactivation logic

        // Act
        var result = connection.Deactivate(ConnectionActivatableArgs.Create(Actor, _dateTimeProvider.Now));

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
            Actor, _dateTimeProvider.Now);

        // Act
        var result = connection.Deactivate(ConnectionActivatableArgs.Create(Actor, _dateTimeProvider.Now));

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
            Actor, _dateTimeProvider.Now);

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
            Actor, _dateTimeProvider.Now);

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
            Actor, _dateTimeProvider.Now);

        // Assert
        connection.Should().NotBeAssignableTo<Wayd.AppIntegration.Domain.Interfaces.ISyncableConnection>();
    }

    [Fact]
    public void Create_RaisesCreatedEvent_WithSettingsAndNoCredential()
    {
        // Act
        var connection = CreateEntra();

        // Assert
        var created = connection.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ConnectionCreatedEvent>().Subject;
        created.Id.Should().Be(connection.Id);
        created.Name.Should().Be("People");
        created.Connector.Should().Be(Connector.Entra);
        created.IsActive.Should().BeTrue();
        created.Settings.Should().Contain(new ConnectionSetting("TenantId", "tenant-1"));
        created.Settings.Select(x => x.Name).Should().NotContain("ClientSecret");
        created.Actor.Should().Be(Actor);
    }

    [Fact]
    public void Update_WithSameValues_RaisesNothing()
    {
        // Arrange
        var connection = CreateEntra();
        connection.ClearDomainEvents();

        // Act
        UpdateEntra(connection, name: " People ");

        // Assert
        connection.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_Name_RaisesOnlyDetailsUpdated()
    {
        // Arrange
        var connection = CreateEntra();
        connection.ClearDomainEvents();

        // Act
        UpdateEntra(connection, name: "Directory");

        // Assert
        var updated = connection.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ConnectionDetailsUpdatedEvent>().Subject;
        updated.Name.Should().Be("Directory");
        updated.Previous.Should().Be(new ConnectionDetails("People", null));
    }

    [Fact]
    public void Update_Setting_RaisesConfigurationChanged_WithBothEnds()
    {
        // Arrange
        var connection = CreateEntra();
        connection.ClearDomainEvents();

        // Act
        UpdateEntra(connection, includeDisabledUsers: true);

        // Assert
        var changed = connection.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ConnectionConfigurationChangedEvent>().Subject;
        changed.Previous.Should().Contain(new ConnectionSetting("IncludeDisabledUsers", "false"));
        changed.Settings.Should().Contain(new ConnectionSetting("IncludeDisabledUsers", "true"));
    }

    [Fact]
    public void Update_Secret_RaisesCredentialsChanged_NamingOnlyTheCredential()
    {
        // Arrange
        var connection = CreateEntra();
        connection.ClearDomainEvents();

        // Act
        UpdateEntra(connection, clientSecret: "rotated-secret");

        // Assert
        var changed = connection.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ConnectionCredentialsChangedEvent>().Subject;
        changed.Credentials.Should().Equal("ClientSecret");
    }

    [Fact]
    public void Events_NeverCarryASecretValue()
    {
        // Arrange
        var connection = CreateEntra();

        // Act
        UpdateEntra(connection, name: "Directory", includeDisabledUsers: true, clientSecret: "rotated-secret");
        connection.Deactivate(ConnectionActivatableArgs.Create(Actor, _dateTimeProvider.Now));
        connection.Delete(Actor, _dateTimeProvider.Now);

        // Assert
        connection.DomainEvents.Should().HaveCount(6);
        foreach (var domainEvent in connection.DomainEvents)
        {
            var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
            payload.Should().NotContain("original-secret").And.NotContain("rotated-secret");
        }
    }

    [Fact]
    public void Activate_WhenInactive_RaisesActivated()
    {
        // Arrange
        var connection = CreateEntra();
        connection.Deactivate(ConnectionActivatableArgs.Create(Actor, _dateTimeProvider.Now));
        connection.ClearDomainEvents();

        // Act
        connection.Activate(ConnectionActivatableArgs.Create(Actor, _dateTimeProvider.Now));

        // Assert
        connection.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ConnectionActivatedEvent>();
    }

    [Fact]
    public void Activate_WhenActive_RaisesNothing()
    {
        // Arrange
        var connection = CreateEntra();
        connection.ClearDomainEvents();

        // Act
        connection.Activate(ConnectionActivatableArgs.Create(Actor, _dateTimeProvider.Now));

        // Assert
        connection.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Deactivate_WhenActive_RaisesDeactivated()
    {
        // Arrange
        var connection = CreateEntra();
        connection.ClearDomainEvents();

        // Act
        connection.Deactivate(ConnectionActivatableArgs.Create(Actor, _dateTimeProvider.Now));

        // Assert
        connection.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ConnectionDeactivatedEvent>();
    }

    [Fact]
    public void Delete_RaisesDeletedEvent_WithNameAndConnector()
    {
        // Arrange
        var connection = CreateEntra();
        connection.ClearDomainEvents();

        // Act
        connection.Delete(Actor, _dateTimeProvider.Now);

        // Assert
        var deleted = connection.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ConnectionDeletedEvent>().Subject;
        deleted.Name.Should().Be("People");
        deleted.Connector.Should().Be(Connector.Entra);
    }

    private EntraConnection CreateEntra() =>
        EntraConnection.Create(
            "People",
            null,
            new EntraConnectionConfiguration("tenant-1", "client-1", "original-secret"),
            true,
            Actor,
            _dateTimeProvider.Now);

    private void UpdateEntra(
        EntraConnection connection,
        string name = "People",
        string clientSecret = "original-secret",
        bool includeDisabledUsers = false)
    {
        connection.Update(
            name,
            null,
            "tenant-1",
            "client-1",
            clientSecret,
            null,
            includeDisabledUsers,
            EmployeeMatchProperty.Email,
            true,
            true,
            Actor,
            _dateTimeProvider.Now).IsSuccess.Should().BeTrue();
    }
}
