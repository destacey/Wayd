using FluentAssertions;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.Common.Application.Interfaces;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.AzureOpenAI;

public class UpdateAzureOpenAIConnectionCommandHandlerTests
{
    private const string StoredApiKey = "stored-aoai-key-000111";
    private const string BaseUrl = "https://ai.acme.example";
    private const string DeploymentName = "acme-deployment";

    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly UpdateAzureOpenAIConnectionCommandHandler _sut;

    public UpdateAzureOpenAIConnectionCommandHandlerTests()
    {
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);

        _sut = _mocker.CreateInstance<UpdateAzureOpenAIConnectionCommandHandler>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_PreservesStoredKey_WhenKeyIsOmitted(string? submittedKey)
    {
        // Arrange
        var connection = CreateConnection();
        var command = new UpdateAzureOpenAIConnectionCommand(
            connection.Id, "Renamed", BaseUrl, "A new description", DeploymentName, submittedKey);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.ApiKey.Should().Be(StoredApiKey,
            "editing an unrelated field must never overwrite the stored credential");
        connection.Description.Should().Be("A new description");
    }

    [Theory]
    [InlineData("********")]
    [InlineData("stor******************")]
    public async Task Handle_PreservesStoredKey_WhenAMaskedValueIsSubmitted(string maskedKey)
    {
        // Arrange
        var connection = CreateConnection();
        var command = new UpdateAzureOpenAIConnectionCommand(
            connection.Id, connection.Name, BaseUrl, connection.Description, DeploymentName, maskedKey);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.ApiKey.Should().Be(StoredApiKey,
            "a caller that posts the masked response back must not overwrite the credential");
    }

    [Fact]
    public async Task Handle_ReplacesStoredKey_WhenANewKeyIsSubmitted()
    {
        // Arrange
        var connection = CreateConnection();
        var newKey = "rotated-aoai-key-999888";
        var command = new UpdateAzureOpenAIConnectionCommand(
            connection.Id, connection.Name, BaseUrl, connection.Description, DeploymentName, newKey);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.ApiKey.Should().Be(newKey);
        _db.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReplacesStoredKey_WhenTheNewKeySharesThePrefixAndLengthOfTheStoredOne()
    {
        // Arrange
        var newKey = "storXd-aoai-key-000111";
        newKey.Length.Should().Be(StoredApiKey.Length);
        var connection = CreateConnection();
        var command = new UpdateAzureOpenAIConnectionCommand(
            connection.Id, connection.Name, BaseUrl, connection.Description, DeploymentName, newKey);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        connection.Configuration.ApiKey.Should().Be(newKey,
            "a rotated key must save even when it happens to match the stored one's prefix and length");
    }

    private AzureOpenAIConnection CreateConnection()
    {
        var connection = AzureOpenAIConnection.Create(
            "Azure OpenAI",
            "Original description",
            new AzureOpenAIConnectionConfiguration(StoredApiKey, DeploymentName, BaseUrl),
            configurationIsValid: true,
            _now);

        _db.AddAzureOpenAIConnection(connection);
        return connection;
    }
}
