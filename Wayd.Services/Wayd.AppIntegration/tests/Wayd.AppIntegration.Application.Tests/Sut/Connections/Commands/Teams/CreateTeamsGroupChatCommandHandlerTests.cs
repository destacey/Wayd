using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.Teams;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.Teams;
using Wayd.Common.Application.Interfaces.Communication;
using Wayd.Common.Application.Requests.AppIntegration;
using Wayd.Common.Domain.Events;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Teams;

public sealed class CreateTeamsGroupChatCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly Mock<ITeamsCommunicationGraphClient> _graph = new();

    [Fact]
    public async Task Handle_CreatesTheChatWithOptionalAndRequiredMembers()
    {
        // Arrange
        _db.AddTeamsConnection(Connection("Teams Prod", valid: true));
        SetupUser("reported@contoso.com", "user-reported");
        SetupUser("extra@contoso.com", "user-extra");
        _graph
            .Setup(g => g.CreateGroupChatAsync(
                It.Is<TeamsGraphCredentials>(c => c.TenantId == "tenant" && c.ClientId == "client"),
                "INC-1 Outage",
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "user-reported", "user-extra" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new TeamsCreatedChat(
                "chat-1",
                "https://teams.microsoft.com/l/chat/chat-1/0")));

        // Act
        var result = await Handler().Handle(
            new CreateTeamsGroupChatCommand(
                "INC-1 Outage",
                ["reported@contoso.com"],
                ["extra@contoso.com"]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Id.Should().Be("chat-1");
        result.Value.Link.Should().Be("https://teams.microsoft.com/l/chat/chat-1/0");
    }

    [Fact]
    public async Task Handle_SkipsAnOptionalPersonTheTenantDoesNotHave()
    {
        // Arrange
        _db.AddTeamsConnection(Connection("Teams Prod", valid: true));
        SetupUser("missing@contoso.com", null);
        SetupUser("extra@contoso.com", "user-extra");
        _graph
            .Setup(g => g.CreateGroupChatAsync(
                It.IsAny<TeamsGraphCredentials>(),
                "INC-1 Outage",
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "user-extra" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new TeamsCreatedChat("chat-1", "https://teams.microsoft.com/l/chat/chat-1/0")));

        // Act
        var result = await Handler().Handle(
            new CreateTeamsGroupChatCommand(
                "INC-1 Outage",
                ["missing@contoso.com"],
                ["extra@contoso.com"]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Id.Should().Be("chat-1");
    }

    [Fact]
    public async Task Handle_DoesNotCreateAChat_WhenARequiredPersonIsMissing()
    {
        // Arrange
        _db.AddTeamsConnection(Connection("Teams Prod", valid: true));
        SetupUser("extra@contoso.com", null);

        // Act
        var result = await Handler().Handle(
            new CreateTeamsGroupChatCommand("INC-1 Outage", [], ["extra@contoso.com"]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No Teams user was found for extra@contoso.com.");
        _graph.Verify(
            g => g.CreateGroupChatAsync(
                It.IsAny<TeamsGraphCredentials>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotCreateAChat_WhenNobodyCanBeResolved()
    {
        // Arrange
        _db.AddTeamsConnection(Connection("Teams Prod", valid: true));
        SetupUser("reported@contoso.com", null);

        // Act
        var result = await Handler().Handle(
            new CreateTeamsGroupChatCommand("INC-1 Outage", ["reported@contoso.com"], []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(
            "None of the incident's people could be found in the Teams tenant, so the chat was not created.");
        _graph.Verify(
            g => g.CreateGroupChatAsync(
                It.IsAny<TeamsGraphCredentials>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_FailsHonestly_WhenNoConnectorCanCreateAChat()
    {
        // Arrange
        var inactive = Connection("Teams Old", valid: true);
        inactive.Deactivate(ConnectionActivatableArgs.Create(EventActor.System, Now));
        _db.AddTeamsConnection(inactive);
        _db.AddTeamsConnection(Connection("Teams Draft", valid: false));
        var deleted = Connection("Teams Gone", valid: true);
        deleted.IsDeleted = true;
        _db.AddTeamsConnection(deleted);

        // Act
        var result = await Handler().Handle(
            new CreateTeamsGroupChatCommand("INC-1 Outage", ["reported@contoso.com"], []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No active Teams connector with a valid configuration can create a chat.");
        _graph.Verify(
            g => g.FindUserIdByEmailAsync(
                It.IsAny<TeamsGraphCredentials>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_FailsHonestly_WhenMoreThanOneConnectorCanCreateAChat()
    {
        // Arrange
        _db.AddTeamsConnection(Connection("Teams Prod", valid: true));
        _db.AddTeamsConnection(Connection("Teams DR", valid: true));

        // Act
        var result = await Handler().Handle(
            new CreateTeamsGroupChatCommand("INC-1 Outage", ["reported@contoso.com"], []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("More than one active Teams connector can create a chat. Leave one active.");
        _graph.Verify(
            g => g.CreateGroupChatAsync(
                It.IsAny<TeamsGraphCredentials>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsTheGraphFailure()
    {
        // Arrange
        _db.AddTeamsConnection(Connection("Teams Prod", valid: true));
        SetupUser("reported@contoso.com", "user-reported");
        _graph
            .Setup(g => g.CreateGroupChatAsync(
                It.IsAny<TeamsGraphCredentials>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<TeamsCreatedChat>("Microsoft Graph could not create the Teams chat: denied."));

        // Act
        var result = await Handler().Handle(
            new CreateTeamsGroupChatCommand("INC-1 Outage", ["reported@contoso.com"], []),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("could not create the Teams chat");
    }

    private CreateTeamsGroupChatCommandHandler Handler() =>
        new(_db, _graph.Object, NullLogger<CreateTeamsGroupChatCommandHandler>.Instance);

    private void SetupUser(string email, string? userId) =>
        _graph
            .Setup(g => g.FindUserIdByEmailAsync(
                It.IsAny<TeamsGraphCredentials>(),
                email,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(userId));

    private static TeamsConnection Connection(string name, bool valid) =>
        TeamsConnection.Create(
            name,
            null,
            new TeamsConnectionConfiguration("tenant", "client", "secret"),
            valid,
            EventActor.System,
            Now);
}
