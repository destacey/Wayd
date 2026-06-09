using FluentAssertions;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;
using Wayd.Tests.Shared;

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
        var initiative = StrategicInitiative.Create(expected.Name, expected.Description, expected.DateRange, expected.PortfolioId);

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
    [InlineData(StrategicInitiativeStatus.Cancelled, false)]
    public void CanBeDeleted_ShouldReturnExpectedBasedOnStatus(StrategicInitiativeStatus status, bool expected)
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.WithStatus(status).Generate();

        // Act & Assert
        initiative.CanBeDeleted().Should().Be(expected);
    }

    [Theory]
    [InlineData(StrategicInitiativeStatus.Completed, true)]
    [InlineData(StrategicInitiativeStatus.Cancelled, true)]
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
    public void AssignRole_ShouldAssignEmployeeToRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker.Generate();

        // Act
        var result = initiative.AssignRole(StrategicInitiativeRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Roles.Should().ContainSingle();
        initiative.Roles.First().Role.Should().Be(StrategicInitiativeRole.Owner);
        initiative.Roles.First().EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public void AssignRole_ShouldFail_WhenEmployeeAlreadyAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker.WithRoles(new Dictionary<StrategicInitiativeRole, HashSet<Guid>>
        {
            { StrategicInitiativeRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = initiative.AssignRole(StrategicInitiativeRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is already assigned to this role.");
    }

    [Fact]
    public void RemoveRole_WithOneRoleAssignment_ShouldRemoveEmployeeFromRoleSuccessfully()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker.WithRoles(new Dictionary<StrategicInitiativeRole, HashSet<Guid>>
        {
            { StrategicInitiativeRole.Owner, new HashSet<Guid> { employeeId } }
        }).Generate();

        // Act
        var result = initiative.RemoveRole(StrategicInitiativeRole.Owner, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Roles.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRole_WithMultipleRoleAssignments_ShouldRemoveEmployeeFromRoleSuccessfully()
    {
        // Arrange
        var employeeId1 = Guid.NewGuid();
        var employeeId2 = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker.WithRoles(new Dictionary<StrategicInitiativeRole, HashSet<Guid>>
        {
            { StrategicInitiativeRole.Owner, new HashSet<Guid> { employeeId1, employeeId2 } }
        }).Generate();

        // Act
        var result = initiative.RemoveRole(StrategicInitiativeRole.Owner, employeeId1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Roles.Count.Should().Be(1);
        initiative.Roles.First().Role.Should().Be(StrategicInitiativeRole.Owner);
        initiative.Roles.First().EmployeeId.Should().Be(employeeId2);
    }

    [Fact]
    public void RemoveRole_ShouldFail_WhenEmployeeNotAssignedToRole()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var initiative = _strategicInitiativeFaker.Generate();

        // Act
        var result = initiative.RemoveRole(StrategicInitiativeRole.Owner, employeeId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Employee is not assigned to this role.");
    }


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
        var result = initiative.UpdateRoles(updatedRoles);

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
        var result = initiative.UpdateRoles(updatedRoles);

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
        var result = initiative.UpdateRoles(updatedRoles);

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
        var result = initiative.UpdateRoles(updatedRoles);

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
        var result = initiative.Approve();

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
        var result = initiative.Activate();

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
        var result = initiative.Activate();

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
        var result = initiative.Complete();

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
        var result = initiative.Complete();

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
        var result = initiative.Cancel();

        // Assert
        result.IsSuccess.Should().BeTrue();
        initiative.Status.Should().Be(StrategicInitiativeStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldFail_WhenStrategicInitiativeIsAlreadyCompletedOrCancelled()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCancelled(_dateTimeProvider);

        // Act
        var result = initiative.Cancel();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The strategic initiative is already completed or cancelled.");
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
        var result = initiative.CreateKpi(expectedKpiParameters);

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
        var result = initiative.CreateKpi(expectedKpiParameters);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("KPIs cannot be created for closed strategic initiatives.");
        initiative.Kpis.Should().BeEmpty();
    }

    [Fact]
    public void CreateKpi_ShouldFail_WhenInCancelledStatus()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCancelled(_dateTimeProvider);
        var expectedKpiParameters = _kpiFaker.Generate().ToUpsertParameters();

        // Act
        var result = initiative.CreateKpi(expectedKpiParameters);

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
        var result = initiative.DeleteKpi(kpi.Id);

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
        var result = initiative.DeleteKpi(kpi.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("KPIs cannot be deleted for closed strategic initiatives.");
        initiative.Kpis.Should().NotBeEmpty();
    }

    [Fact]
    public void DeleteKpi_ShouldFail_WhenInCancelledStatus()
    {
        // Arrange
        var initiative = _strategicInitiativeFaker.AsCancelled(_dateTimeProvider).AddKpis(1);
        var kpi = initiative.Kpis.First();

        // Act
        var result = initiative.DeleteKpi(kpi.Id);

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
        var result = initiative.DeleteKpi(kpi.Id);

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
        var result = initiative.CreateKpi(parameters);

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
        var result = initiative.CreateKpi(parameters);

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
        var result = initiative.DeleteKpi(middleKpi.Id);

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
        var result = initiative.ReorderKpis(reversed);

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
        var result = initiative.ReorderKpis(partial);

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
        var result = initiative.ReorderKpis(ids);

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
        var result = initiative.ReorderKpis(ids);

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
        var result = initiative.ReorderKpis(ids);

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
        var result = initiative.ManageProjects(allProjectIds);

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
        var result = initiative.ManageProjects(remainingProjectIds);

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
        var result = initiative.ManageProjects([expectedProjectId]);

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
        var result = initiative.ManageProjects([Guid.NewGuid()]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Projects cannot be added or removed for closed strategic initiatives.");
        initiative.StrategicInitiativeProjects.Should().HaveCount(3);
    }


    #endregion Project Tests
}