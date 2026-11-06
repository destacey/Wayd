using FluentAssertions;
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
    public async Task Handle_PersistsConnectionWithoutRunningTheCommunicationTest()
    {
        // Arrange
        var command = new CreateTeamsConnectionCommand(
            " Teams ",
            "  ",
            " tenant-id ",
            " client-id ",
            " client-secret ");

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _db.SaveChangesCallCount.Should().Be(1);
        var connection = _db.TeamsConnections.Should().ContainSingle().Subject;
        result.Value.Should().Be(connection.Id);
        connection.Name.Should().Be("Teams");
        connection.Description.Should().BeNull();
        connection.IsValidConfiguration.Should().BeTrue();
        connection.Configuration.TenantId.Should().Be("tenant-id");
        connection.Configuration.ClientId.Should().Be("client-id");
        connection.Configuration.ClientSecret.Should().Be("client-secret");
        _mocker.GetMock<ITeamsCommunicationGraphClient>().Verify(
            c => c.RunHelloWorldTestAsync(
                It.IsAny<TeamsGraphCredentials>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
