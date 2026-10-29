using FluentAssertions;
using CSharpFunctionalExtensions;
using Moq;
using Moq.AutoMock;
using Wayd.AppIntegration.Application.Connections.Commands.Teams;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Teams;

public sealed class VerifyTeamsConnectionCommandHandlerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly VerifyTeamsConnectionCommandHandler _sut;

    public VerifyTeamsConnectionCommandHandlerTests()
    {
        _sut = _mocker.CreateInstance<VerifyTeamsConnectionCommandHandler>();
    }

    [Fact]
    public async Task Handle_WhenUserEmailMissing_ReturnsFailureWithoutCallingGraph()
    {
        // Arrange
        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserEmail())
            .Returns((string?)null);

        // Act
        var result = await _sut.Handle(
            new VerifyTeamsConnectionCommand("tenant", "client", "secret", "assertion"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no email");
        _mocker.GetMock<ITeamsCommunicationGraphClient>()
            .Verify(
                c => c.RunHelloWorldTestAsync(
                    It.IsAny<TeamsGraphCredentials>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserAssertionMissing_ReturnsFailureWithoutCallingGraph()
    {
        // Arrange
        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserEmail())
            .Returns("admin@contoso.com");

        // Act
        var result = await _sut.Handle(
            new VerifyTeamsConnectionCommand("tenant", "client", "secret", "  "),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Microsoft Entra sign-in is required");
        _mocker.GetMock<ITeamsCommunicationGraphClient>()
            .Verify(
                c => c.RunHelloWorldTestAsync(
                    It.IsAny<TeamsGraphCredentials>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact]
    public async Task Handle_WhenGraphTestSucceeds_UsesTrimmedCredentialsEmailAndAssertion()
    {
        // Arrange
        _mocker.GetMock<ICurrentUser>()
            .Setup(u => u.GetUserEmail())
            .Returns("  admin@contoso.com  ");

        _mocker.GetMock<ITeamsCommunicationGraphClient>()
            .Setup(c => c.RunHelloWorldTestAsync(
                It.IsAny<TeamsGraphCredentials>(),
                "admin@contoso.com",
                "assertion-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.Handle(
            new VerifyTeamsConnectionCommand(" tenant ", " client ", " secret ", " assertion-token "),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<ITeamsCommunicationGraphClient>().Verify(
            c => c.RunHelloWorldTestAsync(
                It.Is<TeamsGraphCredentials>(cred =>
                    cred.TenantId == "tenant"
                    && cred.ClientId == "client"
                    && cred.ClientSecret == "secret"),
                "admin@contoso.com",
                "assertion-token",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
