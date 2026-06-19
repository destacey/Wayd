using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Extensions;
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
    public Result AddMember(Employee employee, IReadOnlyList<Guid> roleIds)
    {
        try
        {
            foreach (var roleId in roleIds)
            {
                var result = AddMember(employee, roleId);
                if (result.IsFailure)
                    return Result.Failure(result.Error);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>Adds a member to this team with a single role.</summary>
    public Result<TeamMember> AddMember(Employee employee, Guid roleId)
    {
        try
        {
            Guard.Against.Null(employee);

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
    public Result UpdateMemberRoles(Employee employee, IReadOnlyList<Guid> roleIds)
    {
        try
        {
            Guard.Against.Null(employee);

            var currentRoleIds = _members
                .Where(m => m.EmployeeId == employee.Id && !m.IsDeleted)
                .Select(m => m.RoleId)
                .ToHashSet();

            var requestedRoleIds = roleIds.ToHashSet();

            foreach (var roleId in requestedRoleIds.Except(currentRoleIds))
            {
                var result = AddMember(employee, roleId);
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

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>Removes an employee from this team.</summary>
    public Result RemoveMember(Guid employeeId)
    {
        try
        {
            var memberships = _members.Where(m => m.EmployeeId == employeeId && !m.IsDeleted).ToList();
            if (memberships.Count == 0)
                return Result.Failure("Employee is not a member of this team.");

            foreach (var membership in memberships)
                membership.IsDeleted = true;

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
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
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public Result<TeamMembership> AddTeamMembership(TeamOfTeams parentTeam, MembershipDateRange dateRange, Instant timestamp)
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

            var membership = TeamMembership.Create(Id, parentTeam.Id, dateRange);
            _parentMemberships.Add(membership);

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
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result<TeamMembership> UpdateTeamMembership(Guid membershipId, MembershipDateRange dateRange, Instant timestamp)
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

            membership.Update(dateRange);

            return membership;
        }
        catch (Exception ex)
        {
            return Result.Failure<TeamMembership>(ex.ToString());
        }
    }

    /// <summary>Removes a team membership.</summary>
    /// <param name="membershipId">The membership identifier.</param>
    /// <returns>On success, returns the TeamMembership that was deleted.  This is needed until the EF core bug is fixed.</returns>
    public Result<TeamMembership> RemoveTeamMembership(Guid membershipId)
    {
        try
        {
            var membership = _parentMemberships.Single(m => m.Id == membershipId);

            if (!IsActive)
                return Result.Failure<TeamMembership>($"Memberships can not be removed from inactive teams. {Name} is inactive.");

            if (!membership.Target.IsActive)
                return Result.Failure<TeamMembership>($"Memberships can not be removed from inactive teams. {membership.Target.IsActive} is inactive.");

            _parentMemberships.Remove(membership);

            return Result.Success(membership);
        }
        catch (Exception ex)
        {
            return Result.Failure<TeamMembership>(ex.ToString());
        }
    }
}
