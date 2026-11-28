using FluentAssertions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.Entra;
using Wayd.AppIntegration.Application.Connections.ExternalSystems;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Entra;

public sealed class CreateEntraConnectionCommandHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly CreateEntraConnectionCommandHandler _sut;

    public CreateEntraConnectionCommandHandlerTests()
    {
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.Use<IExternalSystemLinker>(new ExternalSystemLinker(_db, Mock.Of<IAzureDevOpsUrlResolver>()));
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);
        _mocker.GetMock<ICurrentUser>().Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());

        _sut = _mocker.CreateInstance<CreateEntraConnectionCommandHandler>();
    }

    [Fact]
    public async Task Handle_TiesTheNewConnectionToItsTenant()
    {
        // Arrange
        var command = new CreateEntraConnectionCommand("People", null, "tenant-1", "client-1", "secret", null, false, EmployeeMatchProperty.Email, false);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var system = _db.ExternalSystems.Should().ContainSingle().Subject;
        system.Connector.Should().Be(Connector.Entra);
        system.SystemId.Should().Be("tenant-1");
        _db.EntraConnections.Single(c => c.Id == result.Value).ExternalSystemId.Should().Be(system.Id);
        _db.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SharesTheSystem_WithAnotherConnectionToTheSameTenant()
    {
        // Arrange
        var existing = ExternalSystem.Create(Connector.Entra, "tenant-1", "tenant-1", null, EventActor.System, _now);
        _db.AddExternalSystem(existing);
        var command = new CreateEntraConnectionCommand("People 2", null, "tenant-1", "client-2", "secret", null, false, EmployeeMatchProperty.Email, false);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _db.ExternalSystems.Should().ContainSingle();
        _db.EntraConnections.Single(c => c.Id == result.Value).ExternalSystemId.Should().Be(existing.Id);
    }
}
