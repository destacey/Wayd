using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Tests.Data;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class ChangeIntakeDeskScoringModelCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private ChangeIntakeDeskScoringModelCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByADeskAdmin_AssignsTheModel()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        var model = _scenario.ScoringModel();
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskScoringModelCommand(desk.Id, model.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.ScoringModelId.Should().Be(model.Id);
        var raised = desk.DomainEvents.OfType<IntakeDeskScoringModelChangedEvent>().Should().ContainSingle().Subject;
        raised.ToScoringModelName.Should().Be(model.Name);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReplacingAModel_NamesTheOneItReplaces()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var from = _scenario.ScoringModel();
        var to = _scenario.ScoringModel();
        var desk = _scenario.ScoresWith(_scenario.DeskAdministeredBy(deskAdmin.Id), from);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskScoringModelCommand(desk.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var raised = desk.DomainEvents.OfType<IntakeDeskScoringModelChangedEvent>().Should().ContainSingle().Subject;
        raised.FromScoringModelId.Should().Be(from.Id);
        raised.FromScoringModelName.Should().Be(from.Name);
        raised.ToScoringModelId.Should().Be(to.Id);
    }

    [Fact]
    public async Task Handle_WithNoModel_StopsScoring()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.ScoresWith(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.ScoringModel());
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskScoringModelCommand(desk.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.ScoringModelId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdminOffTheDesk_Assigns()
    {
        // Arrange
        var desk = _scenario.Desk();
        var model = _scenario.ScoringModel();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskScoringModelCommand(desk.Id, model.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.ScoringModelId.Should().Be(model.Id);
    }

    [Fact]
    public async Task Handle_ByAnAgent_FailsAndSavesNothing()
    {
        // Arrange — agents score with the model; choosing it is running the desk
        var agent = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent));
        var model = _scenario.ScoringModel();
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskScoringModelCommand(desk.Id, model.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotADeskAdminError);
        desk.ScoringModelId.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithAModelThatDoesNotExist_Fails()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskScoringModelCommand(desk.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChangeIntakeDeskScoringModelCommandHandler.ScoringModelNotFoundError);
    }

    [Fact]
    public async Task Handle_WithAProposedModel_Fails()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        var model = new ScoringModelFaker().AsProposedWsjf();
        _scenario.DbContext.AddScoringModels(model);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskScoringModelCommand(desk.Id, model.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        desk.ScoringModelId.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForADeskThatDoesNotExist_ReportsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(
            new ChangeIntakeDeskScoringModelCommand(Guid.NewGuid(), _scenario.ScoringModel().Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotFoundError);
    }

    [Fact]
    public async Task Handle_WhenTheDeskChangedMeanwhile_ReportsItStale()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);
        _scenario.DbContext.FailNextSaveWith = new DbUpdateConcurrencyException("Revision moved.");

        // Act
        var result = await CreateHandler().Handle(
            new ChangeIntakeDeskScoringModelCommand(desk.Id, _scenario.ScoringModel().Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.StaleError);
    }
}
