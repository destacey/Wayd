using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.Organization.Domain.Tests.Sut.Models;

public class TeamOfTeamsTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly TeamOfTeamsFaker _teamOfTeamsFaker;

    public TeamOfTeamsTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _teamOfTeamsFaker = new();
    }

    #region Create

    [Fact]
    public void Create_WhenValid_Success()
    {
        // Arrange
        var fakeTeamOfTeams = _teamOfTeamsFaker.Generate();

        // Act
        var sut = TeamOfTeams.Create(fakeTeamOfTeams.Name, fakeTeamOfTeams.Code, fakeTeamOfTeams.Description, fakeTeamOfTeams.ActiveDate, _dateTimeProvider.Now);

        // Assert
        sut.Type.Should().Be(TeamType.TeamOfTeams);
        sut.Code.Should().Be(fakeTeamOfTeams.Code);
        sut.Name.Should().Be(fakeTeamOfTeams.Name);
        sut.Description.Should().Be(fakeTeamOfTeams.Description);
        sut.IsActive.Should().BeTrue();
        sut.ParentMemberships.Should().BeEmpty();
        sut.ChildMemberships.Should().BeEmpty();

        sut.PostPersistenceActions.Should().NotBeEmpty();

        // get the first action and execute it to ensure it's a valid TeamCreatedEvent
        var action = sut.PostPersistenceActions.First();
        action.Should().NotBeNull();
        action();
        sut.DomainEvents.Should().NotBeEmpty();
        sut.DomainEvents.Should().ContainSingle(e => e is TeamCreatedEvent);

        // get the event and verify its properties
        var createdEvent = sut.DomainEvents.OfType<TeamCreatedEvent>().First();
        createdEvent.Id.Should().Be(sut.Id);
        createdEvent.Key.Should().Be(sut.Key);
        createdEvent.Code.Should().Be(sut.Code);
        createdEvent.Name.Should().Be(sut.Name);
        createdEvent.Description.Should().Be(sut.Description);
        createdEvent.Type.Should().Be(TeamType.TeamOfTeams);
        createdEvent.ActiveDate.Should().Be(sut.ActiveDate);
        createdEvent.InactiveDate.Should().BeNull();
        createdEvent.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullName_Throws()
    {
        // Arrange
        var fakeTeamOfTeams = _teamOfTeamsFaker.Generate();
        string? name = null;

        // Act
        Action action = () => TeamOfTeams.Create(name!, fakeTeamOfTeams.Code, fakeTeamOfTeams.Description, fakeTeamOfTeams.ActiveDate, _dateTimeProvider.Now);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("Value cannot be null. (Parameter 'Name')");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithInvalidName_Throws(string name)
    {
        // Arrange
        var fakeTeamOfTeams = _teamOfTeamsFaker.Generate();

        // Act
        Action action = () => TeamOfTeams.Create(name, fakeTeamOfTeams.Code, fakeTeamOfTeams.Description, fakeTeamOfTeams.ActiveDate, _dateTimeProvider.Now);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    [Fact]
    public void Create_WithNullCode_Throws()
    {
        // Arrange
        var fakeTeamOfTeams = _teamOfTeamsFaker.Generate();
        TeamCode code = null!;

        // Act
        Action action = () => TeamOfTeams.Create(fakeTeamOfTeams.Name, code, fakeTeamOfTeams.Description, fakeTeamOfTeams.ActiveDate, _dateTimeProvider.Now);

        // Assert
        action.Should().Throw<ArgumentException>().WithMessage("Value cannot be null. (Parameter 'Code')");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidDescription_IsNull(string? description)
    {
        // Arrange
        var fakeTeamOfTeams = _teamOfTeamsFaker.Generate();

        // Act
        var sut = TeamOfTeams.Create(fakeTeamOfTeams.Name, fakeTeamOfTeams.Code, description, fakeTeamOfTeams.ActiveDate, _dateTimeProvider.Now);

        // Assert
        sut.Description.Should().BeNull();
    }

    #endregion Create


    #region Update

    [Fact]
    public void Update_WhenValid_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();

        var name = "New Team Name ";
        var code = new TeamCode("NEWTEST");
        var description = "New Description ";

        // Act
        team.Update(name, code, description, _dateTimeProvider.Now);

        // Assert
        team.Type.Should().Be(TeamType.TeamOfTeams);
        team.Code.Should().Be(code);
        team.Name.Should().Be(name.Trim());
        team.Description.Should().Be(description.Trim());
        team.IsActive.Should().BeTrue();

        team.DomainEvents.Should().NotBeEmpty();
        team.DomainEvents.Should().ContainSingle(e => e is TeamUpdatedEvent);
    }

    [Fact]
    public void Update_WithNullName_ReturnsFailedResult()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        string name = null!;

        // Act
        var result = team.Update(name, team.Code, team.Description, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("System.ArgumentNullException: Value cannot be null. (Parameter 'Name')");

        team.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Update_WithInvalidName_ReturnsFailedResult(string name)
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();

        // Act
        var result = team.Update(name, team.Code, team.Description, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("System.ArgumentException: Required input Name was empty. (Parameter 'Name')");

        team.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithNullCode_ReturnsFailedResult()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        TeamCode code = null!;

        // Act
        var result = team.Update(team.Name, code, team.Description, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("System.ArgumentNullException: Value cannot be null. (Parameter 'Code')");

        team.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Update_WithInvalidDescription_IsNull(string? description)
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();

        // Act
        var result = team.Update(team.Name, team.Code, description, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.Description.Should().BeNull();

        team.DomainEvents.Should().NotBeEmpty();
        team.DomainEvents.Should().ContainSingle(e => e is TeamUpdatedEvent);
    }

    #endregion Update


    #region IActivatable

    [Fact]
    public void Activate_WhenActive_SetsIsActiveToFalse()
    {
        // Arrange
        var inactiveDate = _dateTimeProvider.Now.Minus(Duration.FromDays(10)).InUtc().LocalDateTime.Date;
        var team = _teamOfTeamsFaker.AsInactive(inactiveDate).Generate();

        // Act
        team.Activate(_dateTimeProvider.Now);

        // Assert
        team.IsActive.Should().BeTrue();

        team.DomainEvents.Should().NotBeEmpty();
        team.DomainEvents.Should().ContainSingle(e => e is TeamActivatedEvent);
    }

    [Fact]
    public void Deactivate_WhenActiveAndNoActiveMemberships_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var inactiveDate = _dateTimeProvider.Now.InUtc().LocalDateTime.Date;
        var args = TeamDeactivatableArgs.Create(inactiveDate, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.IsActive.Should().BeFalse();
        team.InactiveDate.Should().Be(inactiveDate);
        team.DomainEvents.Should().ContainSingle(e => e is TeamDeactivatedEvent);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_Failure()
    {
        // Arrange
        var inactiveDate = _dateTimeProvider.Now.Minus(Duration.FromDays(10)).InUtc().LocalDateTime.Date;
        var team = _teamOfTeamsFaker.AsInactive(inactiveDate).Generate();
        var args = TeamDeactivatableArgs.Create(inactiveDate, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The team of teams is already inactive.");
        team.IsActive.Should().BeFalse();
        team.InactiveDate.Should().NotBeNull();
        team.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Deactivate_WhenActiveAndAsOfDateBeforeActiveDate_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var inactiveDate = team.ActiveDate.PlusDays(-5);
        var args = TeamDeactivatableArgs.Create(inactiveDate, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The inactive date cannot be on or before the active date.");
        team.IsActive.Should().BeTrue();
        team.InactiveDate.Should().BeNull();
    }

    [Fact]
    public void Deactivate_WhenActiveAndHasActiveParentMemberships_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var teamOfTeams = _teamOfTeamsFaker.Generate();
        LocalDate start = team.ActiveDate.PlusDays(5);
        LocalDate? end = start.PlusDays(90);
        MembershipDateRange dateRange = new(start, end);
        team.AddTeamMembership(teamOfTeams, dateRange, _dateTimeProvider.Now);

        var inactiveDate = start.PlusDays(10);
        var args = TeamDeactivatableArgs.Create(inactiveDate, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The inactive date must be on or after the end date of the last team membership.");
        team.IsActive.Should().BeTrue();
        team.InactiveDate.Should().BeNull();
    }

    [Fact]
    public void Deactivate_WhenActiveAndHasActiveChildMemberships_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var childTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = team.ActiveDate.PlusDays(5);
        LocalDate? end = start.PlusDays(90);
        MembershipDateRange dateRange = new(start, end);
        childTeam.AddTeamMembership(team, dateRange, _dateTimeProvider.Now);
        var membership = childTeam.ParentMemberships.First();

        team.AddToPrivateList("_childMemberships", membership);

        var inactiveDate = start.PlusDays(10);
        var args = TeamDeactivatableArgs.Create(inactiveDate, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The inactive date must be on or after the end date of the last child team membership.");
        team.IsActive.Should().BeTrue();
        team.InactiveDate.Should().BeNull();
    }

    [Fact]
    public void Deactivate_WhenActiveAndAsOfDateBeforeLatestParentMembershipEnd_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var teamOfTeams = _teamOfTeamsFaker.Generate();
        LocalDate start = team.ActiveDate.PlusDays(5);
        LocalDate? end = start.PlusDays(90);
        MembershipDateRange dateRange = new(start, end);
        team.AddTeamMembership(teamOfTeams, dateRange, _dateTimeProvider.Now);

        var inactiveDate = start.PlusDays(10);
        var args = TeamDeactivatableArgs.Create(inactiveDate, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The inactive date must be on or after the end date of the last team membership.");
        team.IsActive.Should().BeTrue();
        team.InactiveDate.Should().BeNull();
    }

    [Fact]
    public void Deactivate_WhenActiveAndAsOfDateBeforeLatestChildMembershipEnd_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var childTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = team.ActiveDate.PlusDays(5);
        LocalDate? end = start.PlusDays(90);
        MembershipDateRange dateRange = new(start, end);
        childTeam.AddTeamMembership(team, dateRange, _dateTimeProvider.Now);
        var membership = childTeam.ParentMemberships.First();

        team.AddToPrivateList("_childMemberships", membership);

        var inactiveDate = start.PlusDays(10);
        var args = TeamDeactivatableArgs.Create(inactiveDate, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The inactive date must be on or after the end date of the last child team membership.");
        team.IsActive.Should().BeTrue();
        team.InactiveDate.Should().BeNull();
    }

    [Fact]
    public void Deactivate_WhenActiveAndAsOfDateOnLatestParentMembershipEnd_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var teamOfTeams = _teamOfTeamsFaker.Generate();
        LocalDate start = team.ActiveDate.PlusDays(5);
        LocalDate? end = start.PlusDays(90);
        MembershipDateRange dateRange = new(start, end);
        team.AddTeamMembership(teamOfTeams, dateRange, _dateTimeProvider.Now);

        var args = TeamDeactivatableArgs.Create(end.Value, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.IsActive.Should().BeFalse();
        team.InactiveDate.Should().Be(end);
    }

    [Fact]
    public void Deactivate_WhenActiveAndAsOfDateOnLatestChildMembershipEnd_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var childTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = team.ActiveDate.PlusDays(5);
        LocalDate? end = start.PlusDays(90);
        MembershipDateRange dateRange = new(start, end);
        childTeam.AddTeamMembership(team, dateRange, _dateTimeProvider.Now);
        var membership = childTeam.ParentMemberships.First();

        team.AddToPrivateList("_childMemberships", membership);

        var args = TeamDeactivatableArgs.Create(end.Value, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.IsActive.Should().BeFalse();
        team.InactiveDate.Should().Be(end);
    }

    [Fact]
    public void Deactivate_WhenActiveAndAsOfDateAfterLatestParentMembershipEnd_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var teamOfTeams = _teamOfTeamsFaker.Generate();
        LocalDate start = team.ActiveDate.PlusDays(5);
        LocalDate? end = start.PlusDays(90);
        MembershipDateRange dateRange = new(start, end);
        team.AddTeamMembership(teamOfTeams, dateRange, _dateTimeProvider.Now);

        var inactiveDate = start.PlusDays(100);
        var args = TeamDeactivatableArgs.Create(inactiveDate, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.IsActive.Should().BeFalse();
        team.InactiveDate.Should().Be(inactiveDate);
    }

    [Fact]
    public void Deactivate_WhenActiveAndAsOfDateAfterLatestChildMembershipEnd_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var childTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = team.ActiveDate.PlusDays(5);
        LocalDate? end = start.PlusDays(90);
        MembershipDateRange dateRange = new(start, end);
        childTeam.AddTeamMembership(team, dateRange, _dateTimeProvider.Now);
        var membership = childTeam.ParentMemberships.First();

        team.AddToPrivateList("_childMemberships", membership);

        var inactiveDate = start.PlusDays(100);
        var args = TeamDeactivatableArgs.Create(inactiveDate, _dateTimeProvider.Now);

        // Act
        var result = team.Deactivate(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.IsActive.Should().BeFalse();
        team.InactiveDate.Should().Be(inactiveDate);
    }

    #endregion IActivatable


    #region Memberships

    [Fact]
    public void AddTeamToTeamMembership_WhenNoExistingMemberships_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var parentTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = new(2023, 5, 1);
        MembershipDateRange dateRange = new(start, end);

        // Act
        var result = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.ParentMemberships.Count.Should().Be(1);
        team.ParentMemberships.First().SourceId.Should().Be(team.Id);
        team.ParentMemberships.First().TargetId.Should().Be(parentTeam.Id);
        team.ParentMemberships.First().DateRange.Start.Should().Be(start);
        team.ParentMemberships.First().DateRange.End.Should().Be(end);
    }

    [Fact]
    public void AddTeamToTeamMembership_WhenNoExistingMembershipsAndWithNullEnd_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var parentTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = null;
        MembershipDateRange dateRange = new(start, end);

        // Act
        var result = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.ParentMemberships.Count.Should().Be(1);
        team.ParentMemberships.First().SourceId.Should().Be(team.Id);
        team.ParentMemberships.First().TargetId.Should().Be(parentTeam.Id);
        team.ParentMemberships.First().DateRange.Start.Should().Be(start);
        team.ParentMemberships.First().DateRange.End.Should().BeNull();
    }

    [Fact]
    public void AddTeamToTeamMembership_WhenSourceInactive_Failure()
    {
        // Arrange
        var inactiveDate = _dateTimeProvider.Now.Minus(Duration.FromDays(10)).InUtc().LocalDateTime.Date;
        var team = _teamOfTeamsFaker.AsInactive(inactiveDate).Generate();
        var parentTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = null;
        MembershipDateRange dateRange = new(start, end);

        var expectedErrorMessage = $"Memberships can not be added to inactive teams. {team.Name} is inactive.";

        // Act
        var result = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedErrorMessage);
        team.ParentMemberships.Count.Should().Be(0);
    }

    [Fact]
    public void AddTeamToTeamMembership_WhenTargetInactive_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var inactiveDate = _dateTimeProvider.Now.Minus(Duration.FromDays(10)).InUtc().LocalDateTime.Date;
        var parentTeam = _teamOfTeamsFaker.AsInactive(inactiveDate).Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = null;
        MembershipDateRange dateRange = new(start, end);

        var expectedErrorMessage = $"Memberships can not be added to inactive teams. {parentTeam.Name} is inactive.";

        // Act
        var result = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedErrorMessage);
        team.ParentMemberships.Count.Should().Be(0);
    }

    [Fact]
    public void UpdateTeamToTeamMembership_WithValidDates_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var parentTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = new(2023, 5, 1);
        MembershipDateRange dateRange = new(start, end);
        var createresult = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        var membership = team.ParentMemberships.First();
        membership.SetPrivate(m => m.Id, Guid.NewGuid());
        membership.SetPrivate(m => m.Source, team);
        membership.SetPrivate(m => m.Target, parentTeam);

        LocalDate updatedStart = new(2023, 2, 1);
        LocalDate? updatedEnd = new(2023, 5, 10);
        MembershipDateRange updatedDateRange = new(updatedStart, updatedEnd);

        // Act
        var result = team.UpdateTeamMembership(membership.Id, updatedDateRange, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.ParentMemberships.Count.Should().Be(1);
        team.ParentMemberships.First().SourceId.Should().Be(team.Id);
        team.ParentMemberships.First().TargetId.Should().Be(parentTeam.Id);
        team.ParentMemberships.First().DateRange.Start.Should().Be(updatedStart);
        team.ParentMemberships.First().DateRange.End.Should().Be(updatedEnd);
    }

    [Fact]
    public void UpdateTeamToTeamMembership_WhenSourceIsInactive_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var parentTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = new(2023, 5, 1);
        MembershipDateRange dateRange = new(start, end);
        var createresult = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        var membership = team.ParentMemberships.First();
        membership.SetPrivate(m => m.Id, Guid.NewGuid());
        membership.SetPrivate(m => m.Source, team);
        membership.SetPrivate(m => m.Target, parentTeam);

        LocalDate updatedStart = new(2023, 2, 1);
        LocalDate? updatedEnd = new(2023, 5, 10);
        MembershipDateRange updatedDateRange = new(updatedStart, updatedEnd);

        team.SetPrivate(m => m.IsActive, false);

        // Act
        var result = team.UpdateTeamMembership(membership.Id, updatedDateRange, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        team.ParentMemberships.Count.Should().Be(1);
        team.ParentMemberships.First().DateRange.Start.Should().Be(start);
        team.ParentMemberships.First().DateRange.End.Should().Be(end);
    }

    [Fact]
    public void UpdateTeamToTeamMembership_WhenTargetIsInactive_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var parentTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = new(2023, 5, 1);
        MembershipDateRange dateRange = new(start, end);
        var createresult = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        var membership = team.ParentMemberships.First();
        membership.SetPrivate(m => m.Id, Guid.NewGuid());
        membership.SetPrivate(m => m.Source, team);
        membership.SetPrivate(m => m.Target, parentTeam);

        LocalDate updatedStart = new(2023, 2, 1);
        LocalDate? updatedEnd = new(2023, 5, 10);
        MembershipDateRange updatedDateRange = new(updatedStart, updatedEnd);

        parentTeam.SetPrivate(m => m.IsActive, false);

        // Act
        var result = team.UpdateTeamMembership(membership.Id, updatedDateRange, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        team.ParentMemberships.Count.Should().Be(1);
        team.ParentMemberships.First().DateRange.Start.Should().Be(start);
        team.ParentMemberships.First().DateRange.End.Should().Be(end);
    }

    [Fact]
    public void RemoveTeamToTeamMembership_WhenValid_Success()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var parentTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = new(2023, 5, 1);
        MembershipDateRange dateRange = new(start, end);
        var createresult = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        var membership = team.ParentMemberships.First();
        membership.SetPrivate(m => m.Id, Guid.NewGuid());
        membership.SetPrivate(m => m.Source, team);
        membership.SetPrivate(m => m.Target, parentTeam);

        // Act
        var result = team.RemoveTeamMembership(membership.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.ParentMemberships.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveTeamToTeamMembership_WhenSourceIsInactive_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var parentTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = new(2023, 5, 1);
        MembershipDateRange dateRange = new(start, end);
        var createresult = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        var membership = team.ParentMemberships.First();
        membership.SetPrivate(m => m.Id, Guid.NewGuid());
        membership.SetPrivate(m => m.Source, team);
        membership.SetPrivate(m => m.Target, parentTeam);

        team.SetPrivate(m => m.IsActive, false);

        // Act
        var result = team.RemoveTeamMembership(membership.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        team.ParentMemberships.Count.Should().Be(1);
    }

    [Fact]
    public void RemoveTeamToTeamMembership_WhenTargetIsInactive_Failure()
    {
        // Arrange
        var team = _teamOfTeamsFaker.Generate();
        var parentTeam = _teamOfTeamsFaker.Generate();
        LocalDate start = new(2023, 1, 1);
        LocalDate? end = new(2023, 5, 1);
        MembershipDateRange dateRange = new(start, end);
        var createresult = team.AddTeamMembership(parentTeam, dateRange, _dateTimeProvider.Now);

        var membership = team.ParentMemberships.First();
        membership.SetPrivate(m => m.Id, Guid.NewGuid());
        membership.SetPrivate(m => m.Source, team);
        membership.SetPrivate(m => m.Target, parentTeam);

        parentTeam.SetPrivate(m => m.IsActive, false);

        // Act
        var result = team.RemoveTeamMembership(membership.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        team.ParentMemberships.Count.Should().Be(1);
    }


    #endregion Memberships
}
