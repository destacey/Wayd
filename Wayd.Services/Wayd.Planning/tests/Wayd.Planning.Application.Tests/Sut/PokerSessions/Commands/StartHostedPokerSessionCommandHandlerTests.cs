using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public sealed class StartHostedPokerSessionCommandHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 10, 0);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly FakePokerSessionAccess _access = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Common.Domain.Employees.Employee _caller = new EmployeeFaker().Generate();
    private readonly Guid _deskId = Guid.NewGuid();
    private readonly EstimationScale _scale = new EstimationScaleFaker().WithValues("S", "M", "L", "?").Generate();
    private readonly StartHostedPokerSessionCommandHandler _handler;

    public StartHostedPokerSessionCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_caller.Id);
        _currentPrincipal.Setup(p => p.HasPermission(PokerSessionAccess.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
        _dateTimeProvider.Setup(d => d.Now).Returns(Now);
        _dbContext.AddEmployee(_caller);
        _dbContext.AddEstimationScale(_scale);

        _handler = new StartHostedPokerSessionCommandHandler(_dbContext, _access, _currentPrincipal.Object, _currentUser.Object, _dateTimeProvider.Object);
    }

    private static PokerRoundSubjectInput ARequest(string key, string title) =>
        new(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), key, title, "What the requester asked for.");

    private StartHostedPokerSessionCommand Starting(params PokerRoundSubjectInput[] subjects) =>
        new("Desk Refinement", PokerSessionHostKind.IntakeDesk, _deskId, _scale.CurrentVersion.Id, subjects);

    [Fact]
    public async Task Handle_ByADeskManager_StartsAPrivateHostedSessionWithARoundPerRequest()
    {
        // Arrange
        _access.ManagedHostIds.Add(_deskId);
        var first = ARequest("41", "Export invoices");
        var second = ARequest("42", "Single sign-on");

        // Act
        var result = await _handler.Handle(Starting(first, second), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var session = _dbContext.PokerSessions.Should().ContainSingle().Subject;
        session.Id.Should().Be(result.Value.Id);
        session.Host.Should().Be(new PokerSessionHost(PokerSessionHostKind.IntakeDesk, _deskId));
        session.Visibility.Should().Be(Visibility.Private);
        session.EstimationScaleVersionId.Should().Be(_scale.CurrentVersion.Id);
        session.Facilitators.Should().ContainSingle().Which.EmployeeId.Should().Be(_caller.Id);
        session.Rounds.OrderBy(r => r.Order).Select(r => r.Subject!.Title).Should().Equal("Export invoices", "Single sign-on");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnAnEarlierVersionOfTheScale_VotesOnThatVersion()
    {
        // Arrange
        _access.ManagedHostIds.Add(_deskId);
        var pinned = _scale.CurrentVersion;
        _scale.PublishVersion([new("S", true), new("M", true), new("L", true), new("XL", true)], EventActor.System, Now);
        var command = Starting() with { EstimationScaleVersionId = pinned.Id };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.PokerSessions.Single().EstimationScaleVersionId.Should().Be(pinned.Id);
    }

    [Fact]
    public async Task Handle_BySomeoneWhoDoesNotManageTheHost_FailsAndSavesNothing()
    {
        // Act
        var result = await _handler.Handle(Starting(ARequest("41", "Export invoices")), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StartHostedPokerSessionCommandHandler.NotAHostManagerError);
        _dbContext.PokerSessions.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithoutThePokerViewPermission_FailsAndSavesNothing()
    {
        // Arrange — a desk agent who could not open the session they would start
        _access.ManagedHostIds.Add(_deskId);
        _currentPrincipal.Setup(p => p.HasPermission(PokerSessionAccess.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(Starting(ARequest("41", "Export invoices")), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StartHostedPokerSessionCommandHandler.NoPokerAccessError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithAnUnknownVersion_FailsAndSavesNothing()
    {
        // Arrange
        _access.ManagedHostIds.Add(_deskId);
        var command = Starting() with { EstimationScaleVersionId = Guid.NewGuid() };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithTheSameRequestTwice_FailsAndSavesNothing()
    {
        // Arrange
        _access.ManagedHostIds.Add(_deskId);
        var request = ARequest("41", "Export invoices");

        // Act
        var result = await _handler.Handle(Starting(request, request), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.SubjectAlreadyInSessionError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
