using FluentAssertions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.Workday;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Workday;

public class UpdateWorkdayConnectionCommandHandlerTests
{
    private const string StoredIsuPassword = "stored-workday-pass-0001";
    private const string WsdlUrl = "https://wd.acme.example/ccx/service/acme_corp/Staffing/v46.1?wsdl";
    private const string IsuUsername = "wayd_isu@acme_corp";

    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly UpdateWorkdayConnectionCommandHandler _sut;

    /// <summary>The password the init probe was handed, captured so the probe's view of the
    /// credential can be asserted alongside the persisted one.</summary>
    private string? _probedPassword;

    public UpdateWorkdayConnectionCommandHandlerTests()
    {
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);
        _mocker.GetMock<IWorkdayConnectionInitializer>()
            .Setup(i => i.Initialize(It.IsAny<WorkdayRequestContext>(), It.IsAny<CancellationToken>()))
            .Callback<WorkdayRequestContext, CancellationToken>((ctx, _) => _probedPassword = ctx.Credentials.IsuPassword)
            .ReturnsAsync(new ConnectionInitResult(true, 1, [], [], null));

        _sut = _mocker.CreateInstance<UpdateWorkdayConnectionCommandHandler>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_PreservesStoredPassword_WhenPasswordIsOmitted(string? submittedPassword)
    {
        // Arrange
        var connection = CreateConnection();
        var command = CreateCommand(connection.Id, submittedPassword, name: "Renamed", description: "A new description");

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.IsuPassword.Should().Be(StoredIsuPassword,
            "editing an unrelated field must never overwrite the stored credential");
        connection.Description.Should().Be("A new description");
        _probedPassword.Should().Be(StoredIsuPassword,
            "the init probe must run against the real credential, not a blank one");
    }

    [Theory]
    [InlineData("********")]
    [InlineData("stor********************")]
    public async Task Handle_PreservesStoredPassword_WhenAMaskedValueIsSubmitted(string maskedPassword)
    {
        // Arrange
        var connection = CreateConnection();
        var command = CreateCommand(connection.Id, maskedPassword);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.IsuPassword.Should().Be(StoredIsuPassword,
            "a caller that posts the masked response back must not overwrite the credential");
    }

    [Fact]
    public async Task Handle_ReplacesStoredPassword_WhenANewPasswordIsSubmitted()
    {
        // Arrange
        var connection = CreateConnection();
        var newPassword = "rotated-workday-pass-9998";
        var command = CreateCommand(connection.Id, newPassword);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.IsuPassword.Should().Be(newPassword);
        _probedPassword.Should().Be(newPassword);
    }

    [Fact]
    public async Task Handle_ReplacesStoredPassword_WhenTheNewPasswordSharesThePrefixAndLengthOfTheStoredOne()
    {
        // Arrange
        var newPassword = "storXd-workday-pass-0001";
        newPassword.Length.Should().Be(StoredIsuPassword.Length);
        var connection = CreateConnection();
        var command = CreateCommand(connection.Id, newPassword);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        connection.Configuration.IsuPassword.Should().Be(newPassword,
            "a rotated password must save even when it happens to match the stored one's prefix and length");
    }

    private static UpdateWorkdayConnectionCommand CreateCommand(
        Guid id,
        string? isuPassword,
        string name = "Workday",
        string? description = "Original description")
        => new(
            id,
            name,
            description,
            WsdlUrl,
            IsuUsername,
            isuPassword,
            WorkerKey: WorkdayWorkerKey.EmployeeId,
            IncludeInactive: false,
            MatchBy: EmployeeMatchProperty.Email,
            UseUserIdAsEmailFallback: false,
            UsePreferredName: false,
            NormalizeNameCasing: true,
            DepartmentOrganizationTypeId: null,
            OrgExclusions: null);

    private WorkdayConnection CreateConnection()
    {
        var connection = WorkdayConnection.Create(
            "Workday",
            "Original description",
            new WorkdayConnectionConfiguration(WsdlUrl, IsuUsername, StoredIsuPassword),
            configurationIsValid: true,
            _now);

        _db.AddWorkdayConnection(connection);
        return connection;
    }
}
