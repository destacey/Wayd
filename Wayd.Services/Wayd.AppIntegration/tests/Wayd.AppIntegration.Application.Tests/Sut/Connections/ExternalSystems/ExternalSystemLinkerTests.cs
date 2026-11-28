using FluentAssertions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.ExternalSystems;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.ExternalSystems;

public sealed class ExternalSystemLinkerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);
    private static readonly EventActor Actor = EventActor.User(Guid.NewGuid().ToString());

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly ExternalSystemLinker _sut;

    public ExternalSystemLinkerTests()
    {
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.GetMock<IAzureDevOpsUrlResolver>()
            .Setup(r => r.GetOrganizationUrl(It.IsAny<string>()))
            .Returns((string org) => $"https://dev.azure.com/{org}");

        _sut = _mocker.CreateInstance<ExternalSystemLinker>();
    }

    [Fact]
    public async Task Link_CreatesTheSystemFromTheConnection_WhenNoneExists()
    {
        // Arrange
        var connection = AzureDevOps("contoso", "org-id");

        // Act
        var result = await _sut.Link(connection, Actor, _now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var system = _db.ExternalSystems.Should().ContainSingle().Subject;
        system.Connector.Should().Be(Connector.AzureDevOps);
        system.SystemId.Should().Be("org-id");
        system.Name.Should().Be("contoso");
        system.BaseUrl.Should().Be("https://dev.azure.com/contoso");
        connection.ExternalSystemId.Should().Be(system.Id);
        _db.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Link_SharesTheExistingSystem_WhenAnotherConnectionReachesTheSameOrganization()
    {
        // Arrange
        var existing = ExternalSystem.Create(Connector.AzureDevOps, "org-id", "contoso", "https://dev.azure.com/contoso", Actor, _now);
        _db.AddExternalSystem(existing);
        var connection = AzureDevOps("contoso", "org-id");

        // Act
        var result = await _sut.Link(connection, Actor, _now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _db.ExternalSystems.Should().ContainSingle();
        connection.ExternalSystemId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task Link_SharesOneNewSystem_BetweenConnectionsLinkedBeforeASave()
    {
        // Arrange
        var first = Entra("tenant-1");
        var second = Entra("tenant-1");

        // Act
        await _sut.Link(first, Actor, _now, TestContext.Current.CancellationToken);
        await _sut.Link(second, Actor, _now, TestContext.Current.CancellationToken);

        // Assert
        var system = _db.ExternalSystems.Should().ContainSingle().Subject;
        first.ExternalSystemId.Should().Be(system.Id);
        second.ExternalSystemId.Should().Be(system.Id);
    }

    [Fact]
    public async Task Link_GivesPeopleConnectionsASystem()
    {
        // Arrange
        var workday = Workday();
        var entra = Entra("tenant-1");

        // Act
        await _sut.Link(workday, Actor, _now, TestContext.Current.CancellationToken);
        await _sut.Link(entra, Actor, _now, TestContext.Current.CancellationToken);

        // Assert
        var systems = _db.ExternalSystems.ToList();
        systems.Should().HaveCount(2);

        var workdaySystem = systems.Single(s => s.Connector == Connector.Workday);
        workdaySystem.SystemId.Should().Be("acme_corp1");
        workdaySystem.Name.Should().Be("acme_corp1");
        workdaySystem.BaseUrl.Should().Be("https://wd3-impl-services1.workday.com");
        workday.ExternalSystemId.Should().Be(workdaySystem.Id);

        var entraSystem = systems.Single(s => s.Connector == Connector.Entra);
        entraSystem.SystemId.Should().Be("tenant-1");
        entraSystem.BaseUrl.Should().BeNull();
        entra.ExternalSystemId.Should().Be(entraSystem.Id);
    }

    [Fact]
    public async Task Link_RefreshesTheLinkedSystem_WhenTheOrganizationIsRenamed()
    {
        // Arrange
        var connection = AzureDevOps("contoso", "org-id");
        await _sut.Link(connection, Actor, _now, TestContext.Current.CancellationToken);
        var system = _db.ExternalSystems.Single();
        connection.Update(connection.Name, null, "fabrikam", "new-pat", "org-id", true, Actor, _now).IsSuccess.Should().BeTrue();

        // Act
        var result = await _sut.Link(connection, Actor, _now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _db.ExternalSystems.Should().ContainSingle();
        system.Name.Should().Be("fabrikam");
        system.BaseUrl.Should().Be("https://dev.azure.com/fabrikam");
        system.DomainEvents.OfType<ExternalSystemDetailsUpdatedEvent>().Should().ContainSingle()
            .Which.Previous.Name.Should().Be("contoso");
    }

    [Fact]
    public async Task Link_DoesNothing_WhenTheConnectionDoesNotKnowItsSystemId()
    {
        // Arrange
        var connection = AzureDevOps("contoso", systemId: null);

        // Act
        var result = await _sut.Link(connection, Actor, _now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _db.ExternalSystems.Should().BeEmpty();
        connection.ExternalSystemId.Should().BeNull();
    }

    [Fact]
    public async Task Link_DoesNothing_ForAConnectionThatSyncsNothing()
    {
        // Arrange
        var connection = AzureOpenAIConnection.Create(
            "AI",
            null,
            new AzureOpenAIConnectionConfiguration("key", "gpt-4", "https://test.openai.azure.com"),
            true,
            Actor,
            _now);

        // Act
        var result = await _sut.Link(connection, Actor, _now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _db.ExternalSystems.Should().BeEmpty();
        connection.ExternalSystemId.Should().BeNull();
    }

    [Fact]
    public async Task Link_AddsNoSystem_WhenTheConnectionRefusesIt()
    {
        // Arrange
        var connection = Entra("tenant-1");
        var missing = ExternalSystem.Create(Connector.Entra, "tenant-1", "tenant-1", null, Actor, _now);
        connection.LinkExternalSystem(missing, Actor, _now).IsSuccess.Should().BeTrue();

        // Act
        var result = await _sut.Link(connection, Actor, _now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _db.ExternalSystems.Should().BeEmpty("a system the connection refused must not be left for the save to write");
        connection.ExternalSystemId.Should().Be(missing.Id);
    }

    [Fact]
    public async Task Link_RefusesADescriptionTooLongToStore()
    {
        // Arrange
        var connection = AzureDevOps(new string('o', ExternalSystem.NameMaxLength + 1), "org-id");

        // Act
        var result = await _sut.Link(connection, Actor, _now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain($"{ExternalSystem.NameMaxLength} characters");
        _db.ExternalSystems.Should().BeEmpty();
        connection.ExternalSystemId.Should().BeNull();
    }

    private static AzureDevOpsBoardsConnection AzureDevOps(string organization, string? systemId) =>
        AzureDevOpsBoardsConnection.Create("Boards", null, systemId, new AzureDevOpsBoardsConnectionConfiguration(organization, "pat"), systemId is not null, null, Actor, _now);

    private static EntraConnection Entra(string tenantId) =>
        EntraConnection.Create("People", null, new EntraConnectionConfiguration(tenantId, "client-1", "secret"), true, Actor, _now);

    private static WorkdayConnection Workday() =>
        WorkdayConnection.Create(
            "People",
            null,
            new WorkdayConnectionConfiguration("https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1", "isu@acme_corp1", "password"),
            true,
            Actor,
            _now);
}
