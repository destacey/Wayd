using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class ChangeIntakeDeskEstimationScaleCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private ChangeIntakeDeskEstimationScaleCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByADeskAdmin_PinsTheScalesCurrentVersion()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        var scale = _scenario.EstimationScale();
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, scale.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        desk.EstimationScaleVersionId.Should().Be(scale.CurrentVersion.Id);
        var raised = desk.DomainEvents.OfType<IntakeDeskEstimationScaleChangedEvent>().Should().ContainSingle().Subject;
        raised.ToScaleName.Should().Be(scale.Name);
        raised.Actor.EmployeeId.Should().Be(deskAdmin.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReplacingAScale_NamesTheOneItReplaces()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var from = _scenario.EstimationScale("Fibonacci");
        var to = _scenario.EstimationScale("T-Shirt Sizes");
        var desk = _scenario.EstimatesOn(_scenario.DeskAdministeredBy(deskAdmin.Id), from);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var raised = desk.DomainEvents.OfType<IntakeDeskEstimationScaleChangedEvent>().Should().ContainSingle().Subject;
        raised.FromScaleId.Should().Be(from.Id);
        raised.FromScaleName.Should().Be("Fibonacci");
        raised.ToScaleId.Should().Be(to.Id);
    }

    [Fact]
    public async Task Handle_WhileAnOpenRequestHasAnEstimate_FailsAndSavesNothing()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var from = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.DeskAdministeredBy(deskAdmin.Id), from);
        _scenario.Estimated(_scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Active), desk, "M");
        var to = _scenario.EstimationScale("Other");
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDesk.EstimatedOpenRequestsError);
        desk.EstimationScaleVersionId.Should().Be(from.CurrentVersion.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenOnlyClosedRequestsHaveEstimates_Changes()
    {
        // Arrange — a closed request keeps the version it was estimated on
        var deskAdmin = _scenario.Employee();
        var from = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.DeskAdministeredBy(deskAdmin.Id), from);
        var done = new IntakeRequestFaker().WithDeskId(desk.Id).InStatus(StatusCategory.Done)
            .WithEstimate(from, "M").Generate();
        _scenario.DbContext.AddRequests(done);
        var to = _scenario.EstimationScale("Other");
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        desk.EstimationScaleVersionId.Should().Be(to.CurrentVersion.Id);
        done.Estimate!.ScaleVersionId.Should().Be(from.CurrentVersion.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoScale_StopsEstimating()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.EstimationScale());
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        desk.EstimationScaleVersionId.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdminOffTheDesk_Changes()
    {
        // Arrange
        var desk = _scenario.Desk();
        var scale = _scenario.EstimationScale();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, scale.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        desk.EstimationScaleVersionId.Should().Be(scale.CurrentVersion.Id);
    }

    [Fact]
    public async Task Handle_ByAnAgent_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent));
        var scale = _scenario.EstimationScale();
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, scale.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotADeskAdminError);
        desk.EstimationScaleVersionId.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithAScaleWithNoCopy_FailsAndSavesNothing()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChangeIntakeDeskEstimationScaleCommandHandler.EstimationScaleNotFoundError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithAnInactiveScale_FailsAndSavesNothing()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        var scale = _scenario.EstimationScale(isActive: false);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, scale.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDesk.InactiveEstimationScaleError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForADeskThatDoesNotExist_ReportsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(Guid.NewGuid(), null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotFoundError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAnOpenRequestsEstimateIsOnAnOlderScale_Changes()
    {
        // Arrange — a request estimated before the desk last changed scale, closed meanwhile and since reopened
        var deskAdmin = _scenario.Employee();
        var older = _scenario.EstimationScale("Fibonacci");
        var current = _scenario.EstimationScale("T-Shirt Sizes");
        var desk = _scenario.EstimatesOn(_scenario.DeskAdministeredBy(deskAdmin.Id), current);
        var reopened = new IntakeRequestFaker().WithDeskId(desk.Id).InStatus(StatusCategory.Active)
            .WithEstimate(older, "M").Generate();
        _scenario.DbContext.AddRequests(reopened);
        var to = _scenario.EstimationScale("Other");
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskEstimationScaleCommand(desk.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        desk.EstimationScaleVersionId.Should().Be(to.CurrentVersion.Id);
        reopened.Estimate!.ScaleVersionId.Should().Be(older.CurrentVersion.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }
}
