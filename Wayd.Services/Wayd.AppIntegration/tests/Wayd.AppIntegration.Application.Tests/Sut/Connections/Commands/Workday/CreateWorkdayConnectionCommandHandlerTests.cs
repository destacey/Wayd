using FluentAssertions;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.Workday;
using Wayd.AppIntegration.Application.Connections.ExternalSystems;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Workday;

public sealed class CreateWorkdayConnectionCommandHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly CreateWorkdayConnectionCommandHandler _sut;

    public CreateWorkdayConnectionCommandHandlerTests()
    {
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.Use<IExternalSystemLinker>(new ExternalSystemLinker(_db, Mock.Of<IAzureDevOpsUrlResolver>()));
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);
        _mocker.GetMock<ICurrentUser>().Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
        _mocker.GetMock<IWorkdayConnectionInitializer>()
            .Setup(i => i.Initialize(It.IsAny<WorkdayRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionInitResult(true, 1, [], [], null));

        _sut = _mocker.CreateInstance<CreateWorkdayConnectionCommandHandler>();
    }

    [Fact]
    public async Task Handle_TiesTheNewConnectionToItsTenant()
    {
        // Arrange
        var command = new CreateWorkdayConnectionCommand(
            "Workday",
            null,
            "https://wd.acme.example/ccx/service/acme_corp/Staffing/v46.1",
            "isu@acme_corp",
            "password",
            WorkerKey: WorkdayWorkerKey.EmployeeId,
            IncludeInactive: false,
            MatchBy: EmployeeMatchProperty.Email,
            UseUserIdAsEmailFallback: false,
            UsePreferredName: false,
            NormalizeNameCasing: true,
            DepartmentOrganizationTypeId: null,
            OrgExclusions: null);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var system = _db.ExternalSystems.Should().ContainSingle().Subject;
        system.Connector.Should().Be(Connector.Workday);
        system.SystemId.Should().Be("acme_corp");
        system.BaseUrl.Should().Be("https://wd.acme.example");
        _db.WorkdayConnections.Single(c => c.Id == result.Value).ExternalSystemId.Should().Be(system.Id);
    }
}
