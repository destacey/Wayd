using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Extensions;
using Wayd.Common.Interfaces;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Organization.Domain.Models;

public abstract class BaseTeam : BaseSoftDeletableEntity, ISimpleTeam, IHasIdAndKey
{
    protected readonly List<TeamMembership> _parentMemberships = [];
    protected readonly List<TeamMember> _members = [];

    /// <summary>Gets the key.</summary>
    /// <value>The key.</value>
    public int Key { get; protected init; }

    /// <summary>
    /// The name of the team.
    /// </summary>
    public string Name
    {
        get;
        protected set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>Gets the code.</summary>
    /// <value>The code.</value>
    public TeamCode Code
    {
        get;
        protected set => field = Guard.Against.Null(value, nameof(Code));
    } = null!;

    /// <summary>
    /// The description of the team.
    /// </summary>
    public string? Description
    {
        get;
        protected set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>Gets the type.  This value should not change.</summary>
    /// <value>The type.</value>
    public TeamType Type { get; protected set; }

    /// <summary>
    /// The date for when the team became active.
    /// </summary>
    public LocalDate ActiveDate { get; protected set; }

    /// <summary>
    /// The date for when the team became inactive.
    /// </summary>
    public LocalDate? InactiveDate { get; protected set; }

    /// <summary>
    /// Indicates whether the team is active or not.  
    /// </summary>
    public bool IsActive { get; protected set; } = true;

    /// <summary>Gets the parent memberships.</summary>
    /// <value>The parent memberships.</value>
    public IReadOnlyCollection<TeamMembership> ParentMemberships => _parentMemberships.AsReadOnly();

    /// <summary>Gets the members of this team.</summary>
    public IReadOnlyCollection<TeamMember> Members => _members.AsReadOnly();

    /// <summary>Adds a member to this team with one or more roles.</summary>
    public Result AddMember(Employee employee, IReadOnlyList<Guid> roleIds, EventActor actor, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(employee);

            if (roleIds.Count == 0)
                return Result.Failure("At least one role must be specified.");

            var before = MemberRoleIds(employee.Id);

            foreach (var roleId in roleIds.Distinct())
            {
                var result = AddMemberRole(employee, roleId);
                if (result.IsFailure)
                    return Result.Failure(result.Error);
            }

            RaiseMemberRolesChange(employee.Id, before, actor, timestamp);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    private Result<TeamMember> AddMemberRole(Employee employee, Guid roleId)
    {
        try
        {
            if (!IsActive)
                return Result.Failure<TeamMember>($"Members cannot be added to inactive teams. {Name} is inactive.");

            if (!employee.IsActive)
                return Result.Failure<TeamMember>($"Inactive employees cannot be added to teams. {employee.Name.DisplayName} is inactive.");

            if (_members.Any(m => m.EmployeeId == employee.Id && m.RoleId == roleId && !m.IsDeleted))
                return Result.Failure<TeamMember>($"{employee.Name.DisplayName} is already a member of this team in the same role.");

            var member = TeamMember.Create(Id, employee.Id, roleId);
            _members.Add(member);

            return Result.Success(member);
        }
        catch (Exception ex)
        {
            return Result.Failure<TeamMember>(ex.ToString());
        }
    }

    /// <summary>Updates the roles of an existing team member, adding and removing as needed.</summary>
    public Result UpdateMemberRoles(Employee employee, IReadOnlyList<Guid> roleIds, EventActor actor, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(employee);

            var currentRoleIds = MemberRoleIds(employee.Id);

            var requestedRoleIds = roleIds.ToHashSet();

            foreach (var roleId in requestedRoleIds.Except(currentRoleIds))
            {
                var result = AddMemberRole(employee, roleId);
                if (result.IsFailure)
                    return Result.Failure(result.Error);
            }

            foreach (var roleId in currentRoleIds.Except(requestedRoleIds))
            {
                var member = _members.Single(m => m.EmployeeId == employee.Id && m.RoleId == roleId && !m.IsDeleted);
                var result = RemoveMemberRole(member.Id);
                if (result.IsFailure)
                    return Result.Failure(result.Error);
            }

            RaiseMemberRolesChange(employee.Id, currentRoleIds, actor, timestamp);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>Removes an employee from this team.</summary>
    public Result RemoveMember(Guid employeeId, EventActor actor, Instant timestamp)
    {
        try
        {
            var memberships = _members.Where(m => m.EmployeeId == employeeId && !m.IsDeleted).ToList();
            if (memberships.Count == 0)
                return Result.Failure("Employee is not a member of this team.");

            var before = MemberRoleIds(employeeId);

            foreach (var membership in memberships)
                membership.IsDeleted = true;

            RaiseMemberRolesChange(employeeId, before, actor, timestamp);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    private HashSet<Guid> MemberRoleIds(Guid employeeId) =>
        _members
            .Where(m => m.EmployeeId == employeeId && !m.IsDeleted)
            .Select(m => m.RoleId)
            .ToHashSet();

    /// <summary>
    /// Raises the event for how an employee's roles on this team moved from <paramref name="before"/>:
    /// joining from none, leaving for none, or otherwise a change of roles.
    /// </summary>
    private void RaiseMemberRolesChange(Guid employeeId, HashSet<Guid> before, EventActor actor, Instant timestamp)
    {
        var after = MemberRoleIds(employeeId);
        if (after.SetEquals(before))
            return;

        Guid[] roleIds = [.. after];

        if (before.Count == 0)
        {
            AddKeyedDomainEvent(() => new TeamMemberAddedEvent(Id, Key, employeeId, roleIds, actor, timestamp));
        }
        else if (after.Count == 0)
        {
            Guid[] heldRoleIds = [.. before];
            AddKeyedDomainEvent(() => new TeamMemberRemovedEvent(Id, Key, employeeId, heldRoleIds, actor, timestamp));
        }
        else
        {
            Guid[] added = [.. after.Except(before)];
            Guid[] removed = [.. before.Except(after)];
            AddKeyedDomainEvent(() => new TeamMemberRolesChangedEvent(Id, Key, employeeId, added, removed, roleIds, actor, timestamp));
        }
    }

    private Result<TeamMember> RemoveMemberRole(Guid teamMemberId)
    {
        try
        {
            var member = _members.SingleOrDefault(m => m.Id == teamMemberId && !m.IsDeleted);
            if (member is null)
                return Result.Failure<TeamMember>($"Team member with Id {teamMemberId} not found.");

            member.IsDeleted = true;

            return Result.Success(member);
        }
        catch (Exception ex)
        {
            return Result.Failure<TeamMember>(ex.ToString());
        }
    }

    /// <summary>Adds the team membership.</summary>
    /// <param name="parentTeam">The parent team.</param>
    /// <param name="dateRange">The date range.</param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public Result<TeamMembership> AddTeamMembership(TeamOfTeams parentTeam, MembershipDateRange dateRange, EventActor actor, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(parentTeam);
            Guard.Against.Null(dateRange);

            if (!IsActive)
                return Result.Failure<TeamMembership>($"Memberships can not be added to inactive teams. {Name} is inactive.");

            if (!parentTeam.IsActive)
                return Result.Failure<TeamMembership>($"Memberships can not be added to inactive teams. {parentTeam.Name} is inactive.");

            if (_parentMemberships.Any(m => m.DateRange.Overlaps(dateRange)))
                return Result.Failure<TeamMembership>("Teams can only have one active parent Team Membership.  This membership would create an overlapping membership.");

            if (this is TeamOfTeams teamOfTeams)
            {
                var descendantIds = teamOfTeams.GetDescendantTeamIdsAsOf(timestamp.InUtc().Date);
                if (descendantIds.Contains(parentTeam.Id))
                    return Result.Failure<TeamMembership>($"The parent team {parentTeam.Name} is a descendant of this team.  This would create a circular reference.");
            }

            var membership = TeamMembership.Create(this, parentTeam, dateRange);
            _parentMemberships.Add(membership);

            // Both ends, so the hierarchy is consistent as soon as the edge exists rather than only once it
            // has been saved. The cycle check above reads the parent's children, so an edge added earlier in
            // the same batch is only visible if it was recorded here too.
            parentTeam.RecordChildMembership(membership);

            var parentTeamId = parentTeam.Id;
            var range = ToFlexibleDateRange(dateRange);
            AddKeyedDomainEvent(() => new TeamMembershipAddedEvent(Id, Key, parentTeamId, range, actor, timestamp));

            return membership;
        }
        catch (Exception ex)
        {
            return Result.Failure<TeamMembership>(ex.ToString());
        }
    }

    /// <summary>
    /// Updates the team membership.
    /// </summary>
    /// <param name="membershipId"></param>
    /// <param name="dateRange"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result<TeamMembership> UpdateTeamMembership(Guid membershipId, MembershipDateRange dateRange, EventActor actor, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(dateRange);

            var membership = _parentMemberships.Single(m => m.Id == membershipId);

            if (!IsActive)
                return Result.Failure<TeamMembership>($"Memberships can not be updated on inactive teams. {Name} is inactive.");

            if (!membership.Target.IsActive)
                return Result.Failure<TeamMembership>($"Memberships can not be updated on inactive teams. {membership.Target.IsActive} is inactive.");

            if (_parentMemberships.Any(m => m.Id != membershipId && m.DateRange.Overlaps(dateRange)))
                return Result.Failure<TeamMembership>("Teams can only have one active parent Team Membership.  This membership would create an overlapping membership.");

            var previous = membership.DateRange;
            membership.Update(dateRange);
            if (membership.DateRange == previous)
                return membership;

            var parentTeamId = membership.TargetId;
            var range = ToFlexibleDateRange(membership.DateRange);
            var previousRange = ToFlexibleDateRange(previous);
            AddKeyedDomainEvent(() => new TeamMembershipDatesChangedEvent(Id, Key, parentTeamId, range, previousRange, actor, timestamp));

            return membership;
        }
        catch (Exception ex)
        {
            return Result.Failure<TeamMembership>(ex.ToString());
        }
    }

    /// <summary>Removes a team membership.</summary>
    /// <param name="membershipId">The membership identifier.</param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns>On success, returns the TeamMembership that was deleted.  This is needed until the EF core bug is fixed.</returns>
    public Result<TeamMembership> RemoveTeamMembership(Guid membershipId, EventActor actor, Instant timestamp)
    {
        try
        {
            var membership = _parentMemberships.Single(m => m.Id == membershipId);

            if (!IsActive)
                return Result.Failure<TeamMembership>($"Memberships can not be removed from inactive teams. {Name} is inactive.");

            if (!membership.Target.IsActive)
                return Result.Failure<TeamMembership>($"Memberships can not be removed from inactive teams. {membership.Target.IsActive} is inactive.");

            _parentMemberships.Remove(membership);

            var parentTeamId = membership.TargetId;
            var range = ToFlexibleDateRange(membership.DateRange);
            AddKeyedDomainEvent(() => new TeamMembershipRemovedEvent(Id, Key, parentTeamId, range, actor, timestamp));

            return Result.Success(membership);
        }
        catch (Exception ex)
        {
            return Result.Failure<TeamMembership>(ex.ToString());
        }
    }

    protected static FlexibleDateRange ToFlexibleDateRange(IDateRange<LocalDate, LocalDate?> range) => new(range.Start, range.End);

    /// <summary>
    /// Raises an event whose payload carries <see cref="Key"/>. Before the first save the key is still zero
    /// (the team import deactivates a retired team before it is saved), so the raise waits for the save that
    /// assigns it. <paramref name="build"/> runs at that point: capture any other value it reads beforehand.
    /// </summary>
    protected void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
        {
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        }
        else
        {
            AddDomainEvent(build());
        }
    }
}
