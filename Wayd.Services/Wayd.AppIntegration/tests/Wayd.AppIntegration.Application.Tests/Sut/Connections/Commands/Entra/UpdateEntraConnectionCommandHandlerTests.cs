using FluentAssertions;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.Entra;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Entra;

public class UpdateEntraConnectionCommandHandlerTests
{
    private const string StoredClientSecret = "stored-entra-secret-0001";
    private const string TenantId = "acme-tenant-id";
    private const string ClientId = "acme-client-id";

    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly UpdateEntraConnectionCommandHandler _sut;

    public UpdateEntraConnectionCommandHandlerTests()
    {
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);

        _sut = _mocker.CreateInstance<UpdateEntraConnectionCommandHandler>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_PreservesStoredSecret_WhenSecretIsOmitted(string? submittedSecret)
    {
        // Arrange
        var connection = CreateConnection();
        var command = CreateCommand(connection.Id, submittedSecret, name: "Renamed", description: "A new description");

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.ClientSecret.Should().Be(StoredClientSecret,
            "editing an unrelated field must never overwrite the stored credential");
        connection.Description.Should().Be("A new description");
    }

    [Theory]
    [InlineData("********")]
    [InlineData("stor********************")]
    public async Task Handle_PreservesStoredSecret_WhenAMaskedValueIsSubmitted(string maskedSecret)
    {
        // Arrange
        var connection = CreateConnection();
        var command = CreateCommand(connection.Id, maskedSecret);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.ClientSecret.Should().Be(StoredClientSecret,
            "a caller that posts the masked response back must not overwrite the credential");
    }

    [Fact]
    public async Task Handle_ReplacesStoredSecret_WhenANewSecretIsSubmitted()
    {
        // Arrange
        var connection = CreateConnection();
        var newSecret = "rotated-entra-secret-9998";
        var command = CreateCommand(connection.Id, newSecret);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.ClientSecret.Should().Be(newSecret);
        _db.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReplacesStoredSecret_WhenTheNewSecretSharesThePrefixAndLengthOfTheStoredOne()
    {
        // Arrange
        var newSecret = "storXd-entra-secret-0001";
        newSecret.Length.Should().Be(StoredClientSecret.Length);
        var connection = CreateConnection();
        var command = CreateCommand(connection.Id, newSecret);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        connection.Configuration.ClientSecret.Should().Be(newSecret,
            "a rotated secret must save even when it happens to match the stored one's prefix and length");
    }

    private static UpdateEntraConnectionCommand CreateCommand(
        Guid id,
        string? clientSecret,
        string name = "Entra",
        string? description = "Original description")
        => new(
            id,
            name,
            description,
            TenantId,
            ClientId,
            clientSecret,
            AllUsersGroupObjectId: null,
            IncludeDisabledUsers: false,
            MatchBy: EmployeeMatchProperty.Email,
            NormalizeNameCasing: true);

    private EntraConnection CreateConnection()
    {
        var connection = EntraConnection.Create(
            "Entra",
            "Original description",
            new EntraConnectionConfiguration(TenantId, ClientId, StoredClientSecret),
            configurationIsValid: true,
            _now);

        _db.AddEntraConnection(connection);
        return connection;
    }
}
