using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Imports;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Imports;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Imports;

public sealed class IntakeRequestOutcomeImportDefinitionTests
{
    private const int RecordPass = 0;

    private readonly IntakeScenario _scenario = new();

    /// <summary>When the scenario's requests are submitted, which is also now: the earliest and latest an outcome may be.</summary>
    private Instant RecordedAt => _scenario.Clock.Now;
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly IntakeRequestOutcomeImportDefinition _definition;

    private readonly Employee _agent;
    private readonly IntakeDesk _desk;
    private readonly DemandProject _project;

    public IntakeRequestOutcomeImportDefinitionTests()
    {
        _definition = new IntakeRequestOutcomeImportDefinition(
            _scenario.DbContext, _dispatcher.Object, _scenario.User.Object, _scenario.Clock, Mock.Of<IFeatureFlags>(),
            new ImportPayloadSerializer());

        _agent = _scenario.Employee();
        _desk = _scenario.Desk(agentIds: _agent.Id);
        _project = _scenario.Project();
        _dispatcher.Setup(d => d.Send(It.IsAny<GetExistingWorkItemIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
    }

    private ImportIntakeRequestOutcomeDto ScopeRow(Guid requestId, params Guid[] deliverableIds) =>
        new(requestId, IntakeRequestOutcomeKind.ExistingProjectScope, _project.Id, null, "The export screen",
            deliverableIds, _agent.Id, RecordedAt);

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportIntakeRequestOutcomeDto[] rows) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), RecordPass,
            [.. rows.Select((r, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(r)))],
            isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsGatedOnAdministeringDemandManagement()
    {
        // Arrange & Act
        var permission = (_definition.PermissionAction, _definition.PermissionResource);

        // Assert
        permission.Should().Be(("Administer", "IntakeRequests"));
        _definition.Passes.Select(p => p.Name).Should().Equal("RecordIntakeRequestOutcomes");
    }

    [Fact]
    public async Task RecordOutcomes_OnAFulfilledRequest_RecordsTheOutcomeWithItsDeliverables()
    {
        // Arrange — an import records history, so a closed request takes its outcomes
        var request = _scenario.RequestInStatus(_desk, _scenario.Employee(), "Fulfilled");
        var portal = _scenario.Deliverable(_project, "Portal");

        // Act
        var result = await Run(ScopeRow(request.Id, portal.Id));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.Failed.Should().BeFalse(row.Error);
        row.CreatedEntityId.Should().Be(request.Id);
        var outcome = request.Outcomes.Should().ContainSingle().Subject;
        outcome.Should().BeEquivalentTo(new
        {
            Kind = IntakeRequestOutcomeKind.ExistingProjectScope,
            TargetId = _project.Id,
            Note = "The export screen",
            AddedById = _agent.Id,
            AddedOn = RecordedAt,
        });
        outcome.DeliverableIds().Should().Equal(portal.Id);
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestOutcomeAddedEvent>();
    }

    [Fact]
    public async Task RecordOutcomes_WithoutAddedBy_RecordsItAsTheAssigneesAndFailsWithoutOne()
    {
        // Arrange
        var assigned = _scenario.RequestInStatus(_desk, _scenario.Employee(), "Accepted");
        assigned.ChangeAssignee(_agent.Id, Common.Domain.Events.EventActor.System, RecordedAt);
        assigned.ClearDomainEvents();
        var unassigned = _scenario.RequestInStatus(_desk, _scenario.Employee(), "Accepted");

        // Act
        var result = await Run(
            ScopeRow(assigned.Id) with { AddedById = null },
            ScopeRow(unassigned.Id) with { AddedById = null });

        // Assert
        result.Value.Rows[0].Failed.Should().BeFalse(result.Value.Rows[0].Error);
        assigned.Outcomes.Single().AddedById.Should().Be(_agent.Id);
        result.Value.Rows[1].Failed.Should().BeTrue();
        result.Value.Rows[1].Error.Should().Contain("AddedById");
        unassigned.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordOutcomes_WithAnotherProjectsDeliverable_FailsTheRow()
    {
        // Arrange
        var request = _scenario.RequestInStatus(_desk, _scenario.Employee(), "Accepted");
        var theirs = _scenario.Deliverable(_scenario.Project("BOREAL", "Borealis"));

        // Act
        var result = await Run(ScopeRow(request.Id, theirs.Id));

        // Assert
        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.Failed.Should().BeTrue();
        row.Error.Should().StartWith(IntakeRequestAccess.DeliverablesNotOnProjectError).And.Contain(theirs.Id.ToString());
        request.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordOutcomes_ForAProjectWithNoCopy_FailsTheRow()
    {
        // Arrange
        var request = _scenario.RequestInStatus(_desk, _scenario.Employee(), "Accepted");

        // Act
        var result = await Run(ScopeRow(request.Id) with { ProjectId = Guid.NewGuid() });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        result.Value.Rows.Single().Error.Should().StartWith("No project was found");
    }

    [Fact]
    public async Task RecordOutcomes_ForAWorkItem_RecordsOnlyOneWorkHas()
    {
        // Arrange
        var request = _scenario.RequestInStatus(_desk, _scenario.Employee(), "Accepted");
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        _dispatcher.Setup(d => d.Send(It.IsAny<GetExistingWorkItemIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { known });

        // Act
        var result = await Run(
            new ImportIntakeRequestOutcomeDto(request.Id, IntakeRequestOutcomeKind.WorkItem, null, known, null, [], _agent.Id, null),
            new ImportIntakeRequestOutcomeDto(request.Id, IntakeRequestOutcomeKind.WorkItem, null, unknown, null, [], _agent.Id, null));

        // Assert
        result.Value.Rows[0].Failed.Should().BeFalse(result.Value.Rows[0].Error);
        result.Value.Rows[1].Failed.Should().BeTrue();
        result.Value.Rows[1].Error.Should().Contain(unknown.ToString());
        request.Outcomes.Select(o => o.TargetId).Should().Equal(known);
        request.Outcomes.Single().AddedOn.Should().Be(_scenario.Clock.Now);
    }

    [Theory]
    [InlineData(-1, "AddedOn cannot be before the request was submitted.")]
    [InlineData(1, "AddedOn cannot be in the future.")]
    public async Task RecordOutcomes_DatedBeforeTheRequestOrInTheFuture_FailsTheRow(int hours, string error)
    {
        // Arrange — the scenario submits its requests now, so an hour either side is out of bounds.
        var request = _scenario.RequestInStatus(_desk, _scenario.Employee(), "Accepted");

        // Act
        var result = await Run(ScopeRow(request.Id) with { AddedOn = RecordedAt.Plus(Duration.FromHours(hours)) });

        // Assert
        var row = result.Value.Rows.Single();
        row.Failed.Should().BeTrue();
        row.Error.Should().Be(error);
        request.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordOutcomes_ForARequestThatDoesNotExist_FailsTheRow()
    {
        // Arrange & Act
        var result = await Run(ScopeRow(Guid.NewGuid()));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        result.Value.Rows.Single().Error.Should().StartWith("No intake request was found");
    }

    [Fact]
    public async Task RecordOutcomes_ForATargetTheRequestAlreadyPointsAt_FailsTheRow()
    {
        // Arrange
        var request = _scenario.RequestInStatus(_desk, _scenario.Employee(), "Accepted");
        _scenario.Outcome(request, _project.Id, _agent);

        // Act
        var result = await Run(ScopeRow(request.Id));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        result.Value.Rows.Single().Error.Should().Be(IntakeRequest.DuplicateOutcomeError);
        request.Outcomes.Should().ContainSingle();
    }

    [Fact]
    public void Validate_DeliverablesOnAWorkItemRow_IsRefused()
    {
        // Arrange
        var validator = new ImportIntakeRequestOutcomeDtoValidator();

        // Act
        var result = validator.Validate(new ImportIntakeRequestOutcomeDto(
            Guid.NewGuid(), IntakeRequestOutcomeKind.WorkItem, null, Guid.NewGuid(), null, [Guid.NewGuid()], null, null));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ImportIntakeRequestOutcomeDto.DeliverableIds));
    }

    [Fact]
    public void Validate_AScopeRowWithNeitherANoteNorDeliverables_IsRefused()
    {
        // Arrange
        var validator = new ImportIntakeRequestOutcomeDtoValidator();

        // Act
        var result = validator.Validate(ScopeRow(Guid.NewGuid()) with { Note = null });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ImportIntakeRequestOutcomeDto.Note));
    }

    [Fact]
    public void Validate_AScopeRowWithDeliverablesAndNoNote_IsAccepted()
    {
        // Arrange
        var validator = new ImportIntakeRequestOutcomeDtoValidator();

        // Act
        var result = validator.Validate(ScopeRow(Guid.NewGuid(), Guid.NewGuid()) with { Note = null });

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
