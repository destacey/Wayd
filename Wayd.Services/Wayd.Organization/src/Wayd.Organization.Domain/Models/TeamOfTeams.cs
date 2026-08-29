using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Domain.Enums;
using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// A team of teams is a collection of teams and/or other team of teams that aims to help deliver products collaboratively in the same complex environment.
/// </summary>
/// <seealso cref="Wayd.Organization.Domain.Models.BaseTeam" />
/// <seealso cref=""/>
public sealed class TeamOfTeams : BaseTeam, IActivatable<TeamActivatableArgs, TeamDeactivatableArgs>
{
    private readonly List<TeamMembership> _childMemberships = [];

    private TeamOfTeams() { }

    private TeamOfTeams(string name, TeamCode code, string? description, LocalDate activeDate)
    {
        Name = name;
        Code = code;
        Description = description;
        Type = TeamType.TeamOfTeams;
        ActiveDate = activeDate;
    }

    public IReadOnlyCollection<TeamMembership> ChildMemberships => _childMemberships.AsReadOnly();

    /// <summary>
    /// The process for activating a team of teams.
    /// </summary>
    /// <param name="args">The actor making the change and the timestamp of it.</param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(TeamActivatableArgs args)
    {
        if (!IsActive)
        {
            IsActive = true;
            InactiveDate = null;
            AddDomainEvent(new TeamActivatedEvent(Id, args.Actor, args.Timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating a team of teams.
    /// </summary>
    /// <param name="args"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Deactivate(TeamDeactivatableArgs args)
    {
        if (!IsActive)
        {
            return Result.Failure("The team of teams is already inactive.");
        }

        if (args.AsOfDate <= ActiveDate)
        {
            return Result.Failure("The inactive date cannot be on or before the active date.");
        }

        if (ParentMemberships.Count != 0)
        {
            // get the latest membership
            var latestMembership = ParentMemberships.OrderByDescending(x => x.DateRange.Start).First();
            if (latestMembership.DateRange.End == null || args.AsOfDate < latestMembership.DateRange.End.Value)
            {
                return Result.Failure("The inactive date must be on or after the end date of the last team membership.");
            }
        }

        if (ChildMemberships.Count != 0)
        {
            // get the latest membership
            var latestMembership = ChildMemberships.OrderByDescending(x => x.DateRange.Start).First();
            if (latestMembership.DateRange.End == null || args.AsOfDate < latestMembership.DateRange.End.Value)
            {
                return Result.Failure("The inactive date must be on or after the end date of the last child team membership.");
            }
        }

        InactiveDate = args.AsOfDate;
        IsActive = false;  // TODO: this will be invalid if the InactiveDate is in the future

        AddDomainEvent(new TeamDeactivatedEvent(Id, InactiveDate!.Value, args.Actor, args.Timestamp));

        return Result.Success();
    }

    /// <summary>Update team of teams.</summary>
    /// <param name="name">The name.</param>
    /// <param name="code">The code.</param>
    /// <param name="description">The description.</param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public Result Update(string name, TeamCode code, string? description, EventActor actor, Instant timestamp)
    {
        try
        {
            Name = name;
            Code = code;
            Description = description;

            AddDomainEvent(new TeamUpdatedEvent(Id, Code, Name, Description, actor, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>Gets the descendant team ids as of.</summary>
    /// <param name="date">The date.</param>
    /// <param name="includeFuture">if set to <c>true</c> [include future].</param>
    /// <returns></returns>
    public List<Guid> GetDescendantTeamIdsAsOf(LocalDate date, bool includeFuture = false)
    {
        var query = _childMemberships.Where(x => x.StateOn(date) == MembershipState.Active).AsQueryable();
        if (includeFuture)
        {
            query = _childMemberships.Where(x => x.StateOn(date) == MembershipState.Future).AsQueryable();
        }

        List<Guid> descendantTeamIds = [];
        foreach (var membership in _childMemberships)
        {
            if (membership.Source is TeamOfTeams teamOfTeams)
            {
                descendantTeamIds.AddRange(teamOfTeams.GetDescendantTeamIdsAsOf(date, includeFuture));
            }

            descendantTeamIds.Add(membership.SourceId);
        }

        return descendantTeamIds;
    }

    /// <summary>Creates the specified team of teams.</summary>
    /// <param name="name">The name.</param>
    /// <param name="code">The code.</param>
    /// <param name="description">The description.</param>
    /// <param name="activeDate">The active date.</param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public static TeamOfTeams Create(string name, TeamCode code, string? description, LocalDate activeDate, EventActor actor, Instant timestamp)
    {
        var team = new TeamOfTeams(name, code, description, activeDate);

        team.AddPostPersistenceAction(() =>
            team.AddDomainEvent(new TeamCreatedEvent(team.Id, team.Key, team.Code, team.Name, team.Description, team.Type, team.ActiveDate, team.InactiveDate, team.IsActive, actor, timestamp))
        );
        return team;
    }
}
