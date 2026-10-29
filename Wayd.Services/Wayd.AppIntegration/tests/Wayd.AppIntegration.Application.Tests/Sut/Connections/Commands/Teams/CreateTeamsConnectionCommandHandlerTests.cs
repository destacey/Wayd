using FluentAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.Teams;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Teams;

public sealed class CreateTeamsConnectionCommandHandlerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db;
    private readonly CreateTeamsConnectionCommandHandler _sut;

    public CreateTeamsConnectionCommandHandlerTests()
    {
        _db = new FakeAppIntegrationDbContext();
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.GetMock<IDateTimeProvider>()
            .Setup(p => p.Now)
            .Returns(Instant.FromUtc(2026, 1, 1, 0, 0));
        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserId())
            .Returns(Guid.NewGuid().ToString());
        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetEmployeeId())
            .Returns(Guid.NewGuid());
        _sut = _mocker.CreateInstance<CreateTeamsConnectionCommandHandler>();
    }

    [Fact]
    public async Task Handle_WhenGraphTestFails_DoesNotPersistConnection()
    {
        // Arrange
        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserEmail())
            .Returns("admin@contoso.com");

        _mocker.GetMock<ITeamsCommunicationGraphClient>()
            .Setup(c => c.RunHelloWorldTestAsync(
                It.IsAny<TeamsGraphCredentials>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Microsoft Graph rejected the connection test."));

        // Act
        var result = await _sut.Handle(
            new CreateTeamsConnectionCommand(
                "Teams",
                null,
                "tenant-id",
                "client-id",
                "client-secret"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _db.TeamsConnections.Should().BeEmpty();
        _db.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenGraphTestSucceeds_PersistsConnection()
    {
        // Arrange
        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserEmail())
            .Returns("admin@contoso.com");

        _mocker.GetMock<ITeamsCommunicationGraphClient>()
            .Setup(c => c.RunHelloWorldTestAsync(
                It.IsAny<TeamsGraphCredentials>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.Handle(
            new CreateTeamsConnectionCommand(
                "Teams",
                null,
                "tenant-id",
                "client-id",
                "client-secret"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _db.TeamsConnections.Should().HaveCount(1);
        _db.SaveChangesCallCount.Should().Be(1);
    }
}
