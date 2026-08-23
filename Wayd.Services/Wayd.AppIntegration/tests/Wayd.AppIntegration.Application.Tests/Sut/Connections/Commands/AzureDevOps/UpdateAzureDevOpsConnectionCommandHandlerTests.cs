using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections;
using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.AzureDevOps;

public class UpdateAzureDevOpsConnectionCommandHandlerTests
{
    private const string StoredPat = "stored-azdo-pat-000111";

    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly UpdateAzureDevOpsConnectionCommandHandler _sut;

    public UpdateAzureDevOpsConnectionCommandHandlerTests()
    {
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetSystemId(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success("azdo-system-id"));

        _sut = _mocker.CreateInstance<UpdateAzureDevOpsConnectionCommandHandler>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_PreservesStoredToken_WhenTokenIsOmitted(string? submittedPat)
    {
        // Arrange
        var connection = CreateConnection();
        var command = new UpdateAzureDevOpsConnectionCommand(
            connection.Id, "Renamed", "A new description", "acme-org", submittedPat);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.PersonalAccessToken.Should().Be(StoredPat,
            "editing an unrelated field must never overwrite the stored credential");
        connection.Description.Should().Be("A new description");
    }

    [Theory]
    [InlineData("********")]
    [InlineData("stor******************")]
    public async Task Handle_PreservesStoredToken_WhenAMaskedValueIsSubmitted(string maskedPat)
    {
        // Arrange
        var connection = CreateConnection();
        var command = new UpdateAzureDevOpsConnectionCommand(
            connection.Id, connection.Name, connection.Description, "acme-org", maskedPat);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.PersonalAccessToken.Should().Be(StoredPat,
            "a caller that posts the masked response back must not overwrite the credential");
    }

    [Fact]
    public async Task Handle_ReplacesStoredToken_WhenANewTokenIsSubmitted()
    {
        // Arrange
        var connection = CreateConnection();
        var newPat = "rotated-azdo-pat-999888";
        var command = new UpdateAzureDevOpsConnectionCommand(
            connection.Id, connection.Name, connection.Description, "acme-org", newPat);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.PersonalAccessToken.Should().Be(newPat);
        _db.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReplacesStoredToken_WhenTheNewTokenSharesThePrefixAndLengthOfTheStoredOne()
    {
        // Arrange
        var newPat = "storXd-azdo-pat-000111";
        newPat.Length.Should().Be(StoredPat.Length);
        var connection = CreateConnection();
        var command = new UpdateAzureDevOpsConnectionCommand(
            connection.Id, connection.Name, connection.Description, "acme-org", newPat);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        connection.Configuration.PersonalAccessToken.Should().Be(newPat,
            "a rotated token must save even when it happens to match the stored one's prefix and length");
    }

    [Fact]
    public async Task Handle_Fails_WhenConnectionDoesNotExist()
    {
        // Arrange
        var command = new UpdateAzureDevOpsConnectionCommand(
            Guid.CreateVersion7(), "Name", null, "acme-org", "a-token");

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    private AzureDevOpsBoardsConnection CreateConnection()
    {
        var connection = AzureDevOpsBoardsConnection.Create(
            "AzDO",
            "Original description",
            "azdo-system-id",
            new AzureDevOpsBoardsConnectionConfiguration("acme-org", StoredPat),
            configurationIsValid: true,
            teamConfiguration: null,
            _now);

        _db.AddAzureDevOpsBoardsConnection(connection);
        return connection;
    }
}
