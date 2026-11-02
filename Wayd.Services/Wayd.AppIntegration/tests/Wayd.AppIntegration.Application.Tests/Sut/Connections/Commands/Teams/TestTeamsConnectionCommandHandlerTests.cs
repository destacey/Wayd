using FluentAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.Teams;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.Teams;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.Communication;
using Wayd.Common.Domain.Events;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Teams;

public sealed class TestTeamsConnectionCommandHandlerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db;
    private readonly TestTeamsConnectionCommandHandler _sut;

    public TestTeamsConnectionCommandHandlerTests()
    {
        _db = new FakeAppIntegrationDbContext();
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _sut = _mocker.CreateInstance<TestTeamsConnectionCommandHandler>();
    }

    [Fact]
    public async Task Handle_WhenUserEmailMissing_ReturnsFailureWithoutCallingGraph()
    {
        // Arrange
        var connection = CreateTeamsConnection();
        _db.TeamsConnections.Add(connection);

        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserEmail())
            .Returns((string?)null);

        // Act
        var result = await _sut.Handle(
            new TestTeamsConnectionCommand(connection.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no email");
        _mocker.GetMock<ITeamsCommunicationGraphClient>()
            .Verify(
                c => c.RunHelloWorldTestAsync(
                    It.IsAny<TeamsGraphCredentials>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        _db.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenConnectionMissing_ReturnsFailure()
    {
        // Arrange
        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserEmail())
            .Returns("admin@contoso.com");

        // Act
        var result = await _sut.Handle(
            new TestTeamsConnectionCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _db.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenGraphTestSucceeds_UsesSignedInUserEmailAndCredentials()
    {
        // Arrange
        var connection = CreateTeamsConnection();
        _db.TeamsConnections.Add(connection);

        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserEmail())
            .Returns("  admin@contoso.com  ");

        _mocker.GetMock<ITeamsCommunicationGraphClient>()
            .Setup(c => c.RunHelloWorldTestAsync(
                It.IsAny<TeamsGraphCredentials>(),
                "admin@contoso.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.Handle(
            new TestTeamsConnectionCommand(connection.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<ITeamsCommunicationGraphClient>().Verify(
            c => c.RunHelloWorldTestAsync(
                It.Is<TeamsGraphCredentials>(cred =>
                    cred.TenantId == connection.Configuration.TenantId
                    && cred.ClientId == connection.Configuration.ClientId
                    && cred.ClientSecret == connection.Configuration.ClientSecret),
                "admin@contoso.com",
                It.IsAny<CancellationToken>()),
            Times.Once);
        _db.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenGraphCannotFindUser_ReturnsGraphFailure()
    {
        // Arrange
        var connection = CreateTeamsConnection();
        _db.TeamsConnections.Add(connection);

        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserEmail())
            .Returns("missing@contoso.com");

        _mocker.GetMock<ITeamsCommunicationGraphClient>()
            .Setup(c => c.RunHelloWorldTestAsync(
                It.IsAny<TeamsGraphCredentials>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("No user with email 'missing@contoso.com' was found in the Teams connection's tenant."));

        // Act
        var result = await _sut.Handle(
            new TestTeamsConnectionCommand(connection.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("missing@contoso.com");
        _db.SaveChangesCallCount.Should().Be(0);
    }

    private static TeamsConnection CreateTeamsConnection()
    {
        var config = new TeamsConnectionConfiguration(
            "tenant-id",
            "client-id",
            "client-secret");
        return TeamsConnection.Create(
            "Teams test",
            null,
            config,
            configurationIsValid: true,
            EventActor.System,
            Instant.FromUtc(2026, 1, 1, 0, 0));
    }
}
