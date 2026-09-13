using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public sealed class StrategicInitiativeTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly StrategicInitiativeFaker _strategicInitiativeFaker;
    private readonly StrategicInitiativeKpiFaker _kpiFaker;

    public StrategicInitiativeTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _strategicInitiativeFaker = new StrategicInitiativeFaker(_dateTimeProvider);
        _kpiFaker = new StrategicInitiativeKpiFaker();
    }

    [Fact]
    public void Create_ShouldCreateStrategicInitiativeSuccessfully()
    {
        // Arrange
        var expected = _strategicInitiativeFaker.Generate();

        // Act
        var initiative = StrategicInitiative.Create(expected.Name, expected.Description, expected.DateRange, expected.PortfolioId, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.Should().NotBeNull();
        initiative.Name.Should().Be(expected.Name);
        initiative.Description.Should().Be(expected.Description);
        initiative.Status.Should().Be(StrategicInitiativeStatus.Proposed);
        initiative.DateRange.Should().Be(expected.DateRange);
        initiative.PortfolioId.Should().Be(expected.PortfolioId);
        initiative.Roles.Should().BeEmpty();
        initiative.Kpis.Should().BeEmpty();
        initiative.StrategicInitiativeProjects.Should().BeEmpty();
    }

    [Theory]
    [InlineData(StrategicInitiativeStatus.Proposed, true)]
    [InlineData(StrategicInitiativeStatus.Approved, true)]
    [InlineData(StrategicInitiativeStatus.Active, false)]
    [InlineData(StrategicInitiativeStatus.Completed, false)]
    [InlineData(StrategicInitiativeStatus.Canceled, false)]
    public void CanBeDeleted_ShouldReturnExpectedBasedOnStatus(StrategicInitiativeStatus status, bool expected)
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.WithStatus(status).Generate();

        // Act & Assert
        initiative.CanBeDeleted().Should().Be(expected);
    }

    [Theory]
    [InlineData(StrategicInitiativeStatus.Completed, true)]
    [InlineData(StrategicInitiativeStatus.Canceled, true)]
    [InlineData(StrategicInitiativeStatus.Proposed, false)]
    [InlineData(StrategicInitiativeStatus.Approved, false)]
    [InlineData(StrategicInitiativeStatus.Active, false)]
    public void IsClosed_ShouldReturnExpectedBasedOnStatus(StrategicInitiativeStatus status, bool expected)
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.WithStatus(status).Generate();

        // Act & Assert
        initiative.IsClosed.Should().Be(expected);
    }

    #region Roles


    [Fact]
    public void UpdateRoles_ShouldAssignNewRolesSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate();
        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var updatedRoles = new Dictionary<StrategicInitiativeRole, HashSet<Guid>>
        {
            { StrategicInitiativeRole.Sponsor, new HashSet<Guid> { employee1, employee2 } }
        };

        // Act
        var result = initiative.UpdateRoles(updatedRoles, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Roles.Should().Contain(role => role.Role == StrategicInitiativeRole.Sponsor && role.EmployeeId == employee1);
        initiative.Roles.Should().Contain(role => role.Role == StrategicInitiativeRole.Sponsor && role.EmployeeId == employee2);
    }

    [Fact]
    public void UpdateRoles_ShouldRemoveUnspecifiedRoles()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.WithRoles(new Dictionary<StrategicInitiativeRole, HashSet<Guid>>
        {
            { StrategicInitiativeRole.Sponsor, new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() } },
            { StrategicInitiativeRole.Owner, new HashSet<Guid> { Guid.NewGuid() } }
        }).Generate();

        var updatedRoles = new Dictionary<StrategicInitiativeRole, HashSet<Guid>>
        {
            { StrategicInitiativeRole.Sponsor, new HashSet<Guid> { Guid.NewGuid() } }  // Remove Owner role
        };

        // Act
        var result = initiative.UpdateRoles(updatedRoles, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Roles.Should().Contain(role => role.Role == StrategicInitiativeRole.Sponsor);
        initiative.Roles.Should().NotContain(role => role.Role == StrategicInitiativeRole.Owner); // Removed role
    }

    [Fact]
    public void UpdateRoles_ShouldNotChange_WhenRolesAreUnchanged()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker.WithRoles(new Dictionary<StrategicInitiativeRole, HashSet<Guid>>
        {
            { StrategicInitiativeRole.Sponsor, new HashSet<Guid> { employeeId } }
        }).Generate();

        var updatedRoles = new Dictionary<StrategicInitiativeRole, HashSet<Guid>>
        {
            { StrategicInitiativeRole.Sponsor, new HashSet<Guid> { employeeId } }
        };

        // Act
        var result = initiative.UpdateRoles(updatedRoles, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Roles.Count.Should().Be(1);
        initiative.Roles.Should().Contain(role => role.Role == StrategicInitiativeRole.Sponsor && role.EmployeeId == employeeId);
    }

    [Fact]
    public void UpdateRoles_ShouldFail_WhenInvalidRoleProvided()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate();
        var invalidRole = (StrategicInitiativeRole)999;
        var updatedRoles = new Dictionary<StrategicInitiativeRole, HashSet<Guid>>
        {
            { invalidRole, new HashSet<Guid> { Guid.NewGuid() } }
        };

        // Act
        var result = initiative.UpdateRoles(updatedRoles, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Role is not a valid {nameof(StrategicInitiativeRole)} value.");
    }

    #endregion Roles

    #region Lifecycle Tests

    [Fact]
    public void Approve_ShouldApproveProposedStrategicInitiativeSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsProposed(_dateTimeProvider);

        // Act
        var result = initiative.Approve(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Status.Should().Be(StrategicInitiativeStatus.Approved);
    }

    [Fact]
    public void Activate_ShouldActivateApprovedStrategicInitiativeSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsApproved(_dateTimeProvider);

        // Act
        var result = initiative.Activate(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Status.Should().Be(StrategicInitiativeStatus.Active);
    }

    [Fact]
    public void Activate_ShouldFail_WhenStrategicInitiativeIsNotApproved()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsProposed(_dateTimeProvider);

        // Act
        var result = initiative.Activate(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only approved strategic initiatives can be activated.");
    }

    [Fact]
    public void Complete_ShouldCompleteActiveStrategicInitiativeSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider);

        // Act
        var result = initiative.Complete(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Status.Should().Be(StrategicInitiativeStatus.Completed);
    }

    [Fact]
    public void Complete_ShouldFail_WhenStrategicInitiativeIsNotActive()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate();

        // Act
        var result = initiative.Complete(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only active strategic initiatives can be completed.");
    }

    [Fact]
    public void Cancel_ShouldCancelActiveStrategicInitiativeSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider);

        // Act
        var result = initiative.Cancel(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Status.Should().Be(StrategicInitiativeStatus.Canceled);
    }

    [Fact]
    public void Cancel_ShouldFail_WhenStrategicInitiativeIsAlreadyCompletedOrCanceled()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCanceled(_dateTimeProvider);

        // Act
        var result = initiative.Cancel(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The strategic initiative is already completed or canceled.");
    }

    #endregion Lifecycle Tests

    #region KPI Tests

    [Fact]
    public void CreateKpi_ShouldCreateKpiSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate();
        var expectedKpiParameters = _kpiFaker.Generate().ToUpsertParameters();

        // Act
        var result = initiative.CreateKpi(expectedKpiParameters, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Kpis.Should().ContainSingle();

        var kpi = result.Value;
        kpi.Name.Should().Be(expectedKpiParameters.Name);
        kpi.Description.Should().Be(expectedKpiParameters.Description);
        kpi.TargetValue.Should().Be(expectedKpiParameters.TargetValue);
        kpi.Prefix.Should().Be(expectedKpiParameters.Prefix);
        kpi.Suffix.Should().Be(expectedKpiParameters.Suffix);
        kpi.TargetDirection.Should().Be(expectedKpiParameters.TargetDirection);
    }

    [Fact]
    public void CreateKpi_ShouldFail_WhenInCompletedStatus()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCompleted(_dateTimeProvider);
        var expectedKpiParameters = _kpiFaker.Generate().ToUpsertParameters();

        // Act
        var result = initiative.CreateKpi(expectedKpiParameters, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("KPIs cannot be created for closed strategic initiatives.");
        initiative.Kpis.Should().BeEmpty();
    }

    [Fact]
    public void CreateKpi_ShouldFail_WhenInCanceledStatus()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCanceled(_dateTimeProvider);
        var expectedKpiParameters = _kpiFaker.Generate().ToUpsertParameters();

        // Act
        var result = initiative.CreateKpi(expectedKpiParameters, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("KPIs cannot be created for closed strategic initiatives.");
        initiative.Kpis.Should().BeEmpty();
    }

    [Fact]
    public void DeleteKpi_ShouldDeleteKpiSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate().AddKpis(1);
        var kpi = initiative.Kpis.First();

        // Act
        var result = initiative.DeleteKpi(kpi.Id, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Kpis.Should().BeEmpty();
    }

    [Fact]
    public void DeleteKpi_ShouldFail_WhenInCompletedStatus()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCompleted(_dateTimeProvider).AddKpis(1);
        var kpi = initiative.Kpis.First();

        // Act
        var result = initiative.DeleteKpi(kpi.Id, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("KPIs cannot be deleted for closed strategic initiatives.");
        initiative.Kpis.Should().NotBeEmpty();
    }

    [Fact]
    public void DeleteKpi_ShouldFail_WhenInCanceledStatus()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCanceled(_dateTimeProvider).AddKpis(1);
        var kpi = initiative.Kpis.First();

        // Act
        var result = initiative.DeleteKpi(kpi.Id, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("KPIs cannot be deleted for closed strategic initiatives.");
        initiative.Kpis.Should().NotBeEmpty();
    }

    [Fact]
    public void DeleteKpi_ShouldFail_WhenKpiNotFound()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate().AddKpis(1);
        var kpi = _kpiFaker.Generate();

        // Act
        var result = initiative.DeleteKpi(kpi.Id, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("KPI not found.");
        initiative.Kpis.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateKpi_ShouldAssignNextOrder_WhenInitiativeHasNoKpis()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate();
        var parameters = _kpiFaker.Generate().ToUpsertParameters();

        // Act
        var result = initiative.CreateKpi(parameters, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Should().Be(1);
    }

    [Fact]
    public void CreateKpi_ShouldAssignMaxOrderPlusOne_WhenInitiativeHasExistingKpis()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate().AddKpis(3);
        var maxOrder = initiative.Kpis.Max(k => k.Order);
        var parameters = _kpiFaker.Generate().ToUpsertParameters();

        // Act
        var result = initiative.CreateKpi(parameters, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Should().Be(maxOrder + 1);
    }

    [Fact]
    public void DeleteKpi_ShouldResequenceRemainingKpisToEliminateGaps()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate().AddKpis(3);
        var middleKpi = initiative.Kpis.Single(k => k.Order == 2);

        // Act
        var result = initiative.DeleteKpi(middleKpi.Id, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Kpis.Should().HaveCount(2);
        initiative.Kpis.Select(k => k.Order).OrderBy(o => o).Should().Equal(1, 2);
    }

    [Fact]
    public void ReorderKpis_ShouldUpdateOrderToMatchProvidedSequence()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate().AddKpis(3);
        var originalOrder = initiative.Kpis.OrderBy(k => k.Order).Select(k => k.Id).ToList();
        var reversed = originalOrder.AsEnumerable().Reverse().ToList();

        // Act
        var result = initiative.ReorderKpis(reversed, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        for (int i = 0; i < reversed.Count; i++)
        {
            initiative.Kpis.Single(k => k.Id == reversed[i]).Order.Should().Be(i + 1);
        }
    }

    [Fact]
    public void ReorderKpis_ShouldFail_WhenCountDoesNotMatch()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate().AddKpis(3);
        var partial = initiative.Kpis.Take(2).Select(k => k.Id).ToList();

        // Act
        var result = initiative.ReorderKpis(partial, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The number of KPI IDs must match the number of existing KPIs.");
    }

    [Fact]
    public void ReorderKpis_ShouldFail_WhenDuplicateIdsProvided()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate().AddKpis(3);
        var ids = initiative.Kpis.Select(k => k.Id).ToList();
        ids[1] = ids[0]; // duplicate

        // Act
        var result = initiative.ReorderKpis(ids, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Duplicate KPI IDs are not allowed.");
    }

    [Fact]
    public void ReorderKpis_ShouldFail_WhenInitiativeIsClosed()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCompleted(_dateTimeProvider).AddKpis(2);
        var ids = initiative.Kpis.Select(k => k.Id).ToList();

        // Act
        var result = initiative.ReorderKpis(ids, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("KPIs cannot be reordered for closed strategic initiatives.");
    }

    [Fact]
    public void ReorderKpis_ShouldFail_WhenKpiIdNotFound()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate().AddKpis(2);
        var ids = initiative.Kpis.Select(k => k.Id).ToList();
        ids[0] = Guid.NewGuid(); // unknown id

        // Act
        var result = initiative.ReorderKpis(ids, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }


    #endregion KPI Tests

    #region Project Tests

    [Fact]
    public void ManageProjects_ShouldAddProjectsSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddProjects(3, _dateTimeProvider);

        var existingProjectIds = initiative.StrategicInitiativeProjects.Select(p => p.ProjectId).ToArray();
        var newProjectIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var allProjectIds = existingProjectIds.Concat(newProjectIds).ToList();

        // Act
        var result = initiative.ManageProjects(allProjectIds, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.StrategicInitiativeProjects.Should().HaveCount(5);
        initiative.StrategicInitiativeProjects.Should().OnlyContain(p => allProjectIds.Contains(p.ProjectId));
    }

    [Fact]
    public void ManageProjects_ShouldRemoveProjectsSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddProjects(3, _dateTimeProvider);

        var existingProjectIds = initiative.StrategicInitiativeProjects.Select(p => p.ProjectId).ToArray();

        // remove one project
        var projectToRemove = existingProjectIds.First();
        var remainingProjectIds = existingProjectIds.Where(id => id != projectToRemove).ToList();

        // Act
        var result = initiative.ManageProjects(remainingProjectIds, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.StrategicInitiativeProjects.Should().HaveCount(2);
        initiative.StrategicInitiativeProjects.Should().OnlyContain(p => remainingProjectIds.Contains(p.ProjectId));
    }

    [Fact]
    public void ManageProjects_ShouldAddAndRemoveProjectsSuccessfully()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddProjects(3, _dateTimeProvider);

        var expectedProjectId = Guid.NewGuid();

        // Act
        var result = initiative.ManageProjects([expectedProjectId], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.StrategicInitiativeProjects.Should().HaveCount(1);
        initiative.StrategicInitiativeProjects.Should().ContainSingle(p => p.ProjectId == expectedProjectId);
    }

    [Fact]
    public void ManageProjects_ShouldFail_WhenInCompletedStatus()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCompleted(_dateTimeProvider).AddProjects(3, _dateTimeProvider);

        // Act
        var result = initiative.ManageProjects([Guid.NewGuid()], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Projects cannot be added or removed for closed strategic initiatives.");
        initiative.StrategicInitiativeProjects.Should().HaveCount(3);
    }


    #endregion Project Tests

    #region Domain Events

    [Fact]
    public void UpdateDetails_RaisesDetailsUpdated_CarryingWhatItReplaced()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.WithName("Atlas").WithDescription("Before").Generate();

        // Act
        var result = initiative.UpdateDetails("Atlas 2", "After", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeDetailsUpdatedEvent>().Subject;
        raised.Id.Should().Be(initiative.Id);
        raised.Key.Should().Be(initiative.Key);
        raised.Name.Should().Be("Atlas 2");
        raised.Previous.Should().Be(new StrategicInitiativeDetails("Atlas", "Before"));
        raised.AggregateType.Should().Be("StrategicInitiative");
    }

    [Fact]
    public void UpdateDetails_RaisesNothing_WhenOnlyWhitespaceDiffers()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.WithName("Atlas").WithDescription("Before").Generate();

        // Act
        initiative.UpdateDetails("Atlas ", " Before", EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateTimeline_RaisesTimelineChanged_WithBothEnds()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate();
        var previous = initiative.DateRange;
        var moved = new LocalDateRange(previous.Start, previous.End.PlusDays(30));

        // Act
        initiative.UpdateTimeline(moved, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeTimelineChangedEvent>().Subject;
        raised.PreviousDateRange.Should().Be(previous);
        raised.DateRange.Should().Be(moved);
    }

    [Fact]
    public void UpdateTimeline_RaisesNothing_WhenTheRangeIsUnchanged()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.Generate();

        // Act
        initiative.UpdateTimeline(new LocalDateRange(initiative.DateRange.Start, initiative.DateRange.End), EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateRoles_RaisesRolesChanged_WithTheChangeAndTheRosterAfterwards()
    {
        // Arrange
        var keptOwner = Guid.NewGuid();
        var removedSponsor = Guid.NewGuid();
        var addedSponsor = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker
            .WithRoles(new() { [StrategicInitiativeRole.Owner] = [keptOwner], [StrategicInitiativeRole.Sponsor] = [removedSponsor] })
            .Generate();

        // Act
        initiative.UpdateRoles(
            new() { [StrategicInitiativeRole.Owner] = [keptOwner], [StrategicInitiativeRole.Sponsor] = [addedSponsor] },
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeRolesChangedEvent>().Subject;
        raised.Added.Should().Equal(new RoleAssignmentChange((int)StrategicInitiativeRole.Sponsor, addedSponsor));
        raised.Removed.Should().Equal(new RoleAssignmentChange((int)StrategicInitiativeRole.Sponsor, removedSponsor));
        raised.Roles[(int)StrategicInitiativeRole.Owner].Should().Equal(keptOwner);
        raised.Roles[(int)StrategicInitiativeRole.Sponsor].Should().Equal(addedSponsor);
    }

    [Fact]
    public void UpdateRoles_RaisesNothing_WhenTheRosterIsUnchanged()
    {
        // Arrange
        var owner = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker.WithRoles(new() { [StrategicInitiativeRole.Owner] = [owner] }).Generate();

        // Act
        initiative.UpdateRoles(new() { [StrategicInitiativeRole.Owner] = [owner] }, EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Approve_RaisesStatusChanged_WithBothEnds()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsProposed(_dateTimeProvider);

        // Act
        initiative.Approve(EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeStatusChangedEvent>().Subject;
        raised.FromStatus.Should().Be(nameof(StrategicInitiativeStatus.Proposed));
        raised.FromCategory.Should().Be(LifecycleCategory.NotStarted);
        raised.ToStatus.Should().Be(nameof(StrategicInitiativeStatus.Approved));
        raised.ToCategory.Should().Be(LifecycleCategory.NotStarted);
    }

    [Fact]
    public void ARefusedTransition_RaisesNothing()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCompleted(_dateTimeProvider);

        // Act
        var result = initiative.Cancel(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        initiative.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CreateKpi_RaisesKpiAdded_DescribingTheKpi()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider);
        var parameters = _kpiFaker.Generate().ToUpsertParameters();

        // Act
        var kpi = initiative.CreateKpi(parameters, EventActor.System, _dateTimeProvider.Now).Value;

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeKpiAddedEvent>().Subject;
        raised.KpiId.Should().Be(kpi.Id);
        raised.Name.Should().Be(kpi.Name);
        raised.TargetValue.Should().Be(kpi.TargetValue);
        raised.TargetDirection.Should().Be(kpi.TargetDirection);
        raised.Order.Should().Be(1);
    }

    [Fact]
    public void UpdateKpi_RaisesOnlyDetailsUpdated_WhenOnlyTheLabelChanged()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddKpis(1);
        var kpi = initiative.Kpis.Single();
        var previousName = kpi.Name;

        // Act
        initiative.UpdateKpi(kpi.Id, kpi.ToUpsertParameters() with { Name = "Renamed" }, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeKpiDetailsUpdatedEvent>().Subject;
        raised.Name.Should().Be("Renamed");
        raised.Previous.Name.Should().Be(previousName);
    }

    [Fact]
    public void UpdateKpi_RaisesOnlyTargetChanged_WhenOnlyTheTargetMoved()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddKpis(1);
        var kpi = initiative.Kpis.Single();
        var previousTarget = kpi.TargetValue;

        // Act
        initiative.UpdateKpi(kpi.Id, kpi.ToUpsertParameters() with { TargetValue = previousTarget + 10 }, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeKpiTargetChangedEvent>().Subject;
        raised.TargetValue.Should().Be(previousTarget + 10);
        raised.Previous.TargetValue.Should().Be(previousTarget);
    }

    [Fact]
    public void UpdateKpi_RaisesNothing_WhenNothingChanged()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddKpis(1);
        var kpi = initiative.Kpis.Single();

        // Act
        initiative.UpdateKpi(kpi.Id, kpi.ToUpsertParameters() with { Name = kpi.Name + " " }, EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DeleteKpi_RaisesKpiRemoved_CarryingTheName()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddKpis(2);
        var kpi = initiative.Kpis.First();

        // Act
        initiative.DeleteKpi(kpi.Id, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeKpiRemovedEvent>().Subject;
        raised.KpiId.Should().Be(kpi.Id);
        raised.Name.Should().Be(kpi.Name);
    }

    [Fact]
    public void ReorderKpis_RaisesKpisReordered_WithBothOrders()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddKpis(3);
        var previous = initiative.Kpis.OrderBy(k => k.Order).Select(k => k.Id).ToList();
        var reversed = Enumerable.Reverse(previous).ToList();

        // Act
        initiative.ReorderKpis(reversed, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeKpisReorderedEvent>().Subject;
        raised.PreviousOrder.Should().Equal(previous);
        raised.Order.Should().Equal(reversed);
    }

    [Fact]
    public void ReorderKpis_RaisesNothing_WhenTheOrderIsUnchanged()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddKpis(3);
        var current = initiative.Kpis.OrderBy(k => k.Order).Select(k => k.Id).ToList();

        // Act
        initiative.ReorderKpis(current, EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ManageKpiCheckpointPlan_RaisesPlanChanged_WithAddedRemovedAndRevised()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddKpis(1);
        var kpi = initiative.Kpis.Single();
        var q1 = _dateTimeProvider.Now.Plus(Duration.FromDays(30));
        var q2 = _dateTimeProvider.Now.Plus(Duration.FromDays(60));
        var q3 = _dateTimeProvider.Now.Plus(Duration.FromDays(90));
        initiative.ManageKpiCheckpointPlan(kpi.Id,
            [UpsertStrategicInitiativeKpiCheckpoint.Create(null, 10, q1, "Q1"), UpsertStrategicInitiativeKpiCheckpoint.Create(null, 20, q2, "Q2")],
            EventActor.System, _dateTimeProvider.Now);
        var revisedId = kpi.Checkpoints.Single(c => c.DateLabel == "Q1").Id;
        var removedId = kpi.Checkpoints.Single(c => c.DateLabel == "Q2").Id;
        initiative.ClearDomainEvents();

        // Act
        initiative.ManageKpiCheckpointPlan(kpi.Id,
            [UpsertStrategicInitiativeKpiCheckpoint.Create(revisedId, 15, q1, "Q1"), UpsertStrategicInitiativeKpiCheckpoint.Create(null, 30, q3, "Q3")],
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeKpiCheckpointPlanChangedEvent>().Subject;
        raised.Added.Should().ContainSingle().Which.DateLabel.Should().Be("Q3");
        raised.Removed.Should().ContainSingle().Which.CheckpointId.Should().Be(removedId);
        var revision = raised.Revised.Should().ContainSingle().Subject;
        revision.Previous.TargetValue.Should().Be(10);
        revision.Current.TargetValue.Should().Be(15);
        raised.Checkpoints.Select(c => c.DateLabel).Should().Equal("Q1", "Q3");
    }

    [Fact]
    public void ManageKpiCheckpointPlan_RaisesNothing_WhenThePlanIsUnchanged()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddKpis(1);
        var kpi = initiative.Kpis.Single();
        var q1 = _dateTimeProvider.Now.Plus(Duration.FromDays(30));
        initiative.ManageKpiCheckpointPlan(kpi.Id, [UpsertStrategicInitiativeKpiCheckpoint.Create(null, 10, q1, "Q1")], EventActor.System, _dateTimeProvider.Now);
        var checkpointId = kpi.Checkpoints.Single().Id;
        initiative.ClearDomainEvents();

        // Act
        initiative.ManageKpiCheckpointPlan(kpi.Id, [UpsertStrategicInitiativeKpiCheckpoint.Create(checkpointId, 10, q1, "Q1 ")], EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddAndRemoveKpiMeasurement_RaiseTheirEvents()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider).AddKpis(1);
        var kpi = initiative.Kpis.Single();
        var measuredBy = Guid.NewGuid();
        var measurement = StrategicInitiativeKpiMeasurement.Create(
            kpi.Id, 42, _dateTimeProvider.Now.Minus(Duration.FromDays(1)), measuredBy, "Month end.", _dateTimeProvider.Now).Value;

        // Act
        initiative.AddKpiMeasurement(kpi.Id, measurement, EventActor.System, _dateTimeProvider.Now);
        initiative.RemoveKpiMeasurement(kpi.Id, measurement.Id, EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.DomainEvents.Should().HaveCount(2);
        var added = initiative.DomainEvents.First().Should().BeOfType<StrategicInitiativeKpiMeasurementAddedEvent>().Subject;
        added.MeasurementId.Should().Be(measurement.Id);
        added.ActualValue.Should().Be(42);
        added.MeasuredById.Should().Be(measuredBy);
        var removed = initiative.DomainEvents.Last().Should().BeOfType<StrategicInitiativeKpiMeasurementRemovedEvent>().Subject;
        removed.MeasurementId.Should().Be(measurement.Id);
        removed.ActualValue.Should().Be(42);
    }

    [Fact]
    public void ManageProjects_RaisesProjectsChanged_WithTheChangeAndTheSetAfterwards()
    {
        // Arrange
        var kept = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var added = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider);
        initiative.ManageProjects([kept, removed], EventActor.System, _dateTimeProvider.Now);
        initiative.ClearDomainEvents();

        // Act
        initiative.ManageProjects([kept, added], EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = initiative.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<StrategicInitiativeProjectsChangedEvent>().Subject;
        raised.Added.Should().Equal(added);
        raised.Removed.Should().Equal(removed);
        raised.ProjectIds.Should().BeEquivalentTo([kept, added]);
    }

    [Fact]
    public void ManageProjects_RaisesNothing_WhenTheSetIsUnchanged()
    {
        // Arrange
        var project = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker.AsActive(_dateTimeProvider);
        initiative.ManageProjects([project], EventActor.System, _dateTimeProvider.Now);
        initiative.ClearDomainEvents();

        // Act
        initiative.ManageProjects([project], EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_ThenMutatedBeforeTheFirstSave_RecordsTheInitiativeAsCreated()
    {
        // An import creates an initiative and walks it to its status before the save that assigns its key.
        // Arrange
        var dateRange = new LocalDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(90));
        var initiative = StrategicInitiative.Create("Atlas", "Before", dateRange, Guid.NewGuid(), null, EventActor.System, _dateTimeProvider.Now);

        // Act
        initiative.Approve(EventActor.System, _dateTimeProvider.Now);
        initiative.CreateKpi(_kpiFaker.Generate().ToUpsertParameters(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        initiative.DomainEvents.Should().BeEmpty("every event carries the key, which the first save assigns");

        initiative.SetPrivate(i => i.Key, 42);
        initiative.ExecutePostPersistenceActions();

        initiative.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(StrategicInitiativeCreatedEvent),
            typeof(StrategicInitiativeStatusChangedEvent),
            typeof(StrategicInitiativeKpiAddedEvent));
        var created = (StrategicInitiativeCreatedEvent)initiative.DomainEvents.First();
        created.Key.Should().Be(42);
        created.Status.Should().Be((int)StrategicInitiativeStatus.Proposed);
        created.AggregateId.Should().Be(initiative.Id);
        initiative.DomainEvents.OfType<StrategicInitiativeStatusChangedEvent>().Single().Key.Should().Be(42);
    }

    #endregion Domain Events
}
