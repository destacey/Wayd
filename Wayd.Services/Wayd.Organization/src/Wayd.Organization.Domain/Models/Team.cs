using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Domain.Enums;
using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// A team is a collection of team members that work together to execute against a prioritized set of goals.
/// </summary>
/// <seealso cref="Wayd.Organization.Domain.Models.BaseTeam" />
/// <seealso cref="Wayd.Common.Domain.Interfaces.Organization.IActivatable{NodaTime.Instant, Wayd.Organization.Domain.Models.TeamDeactivatableArgs}" />"/>
public sealed class Team : BaseTeam, IActivatable<TeamActivatableArgs, TeamDeactivatableArgs>, IHasTeamIdAndCode
{
    private readonly List<TeamOperatingModel> _operatingModels = [];

    private Team() { }

    private Team(string name, TeamCode code, string? description, LocalDate activeDate)
    {
        Name = name;
        Code = code;
        Description = description;
        Type = TeamType.Team;
        ActiveDate = activeDate;
    }

    /// <summary>Gets the operating models for this team.</summary>
    public IReadOnlyCollection<TeamOperatingModel> OperatingModels => _operatingModels.AsReadOnly();

    /// <summary>
    /// The process for activating an organization.
    /// </summary>
    /// <param name="args">The actor making the change and the timestamp of it.</param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(TeamActivatableArgs args)
    {
        if (!IsActive)
        {
            // TODO is there logic that would prevent activation?
            IsActive = true;
            InactiveDate = null;
            AddDomainEvent(new TeamActivatedEvent(Id, args.Actor, args.Timestamp));

        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating an organization.
    /// </summary>
    /// <param name="args"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Deactivate(TeamDeactivatableArgs args)
    {
        if (!IsActive)
        {
            return Result.Failure("The team is already inactive.");
        }

        if (args.AsOfDate <= ActiveDate)
        {
            return Result.Failure("The inactive date cannot be on or before the active date.");
        }

        if (ParentMemberships.Count != 0)
        {
            var latestMembership = ParentMemberships.OrderByDescending(x => x.DateRange.Start).First();
            if (latestMembership.DateRange.End == null || args.AsOfDate < latestMembership.DateRange.End.Value)
            {
                return Result.Failure("The inactive date must be on or after the end date of the last team membership.");
            }
        }

        InactiveDate = args.AsOfDate;
        IsActive = false;  // TODO: this will be invalid if the InactiveDate is in the future

        AddDomainEvent(new TeamDeactivatedEvent(Id, InactiveDate!.Value, args.Actor, args.Timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Update team
    /// </summary>
    /// <param name="name"></param>
    /// <param name="code"></param>
    /// <param name="description"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result Update(string name, TeamCode code, string? description, EventActor actor, Instant timestamp)
    {
        try
        {
            Name = name;
            Code = code;
            Description = description;

            // Publish specific TeamUpdatedEvent immediately; Id is already set prior to updates
            AddDomainEvent(new TeamUpdatedEvent(Id, Code, Name, Description, actor, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>
    /// Sets a new operating model for the team. If a current model exists, it will be closed
    /// with an end date of one day before the new model's start date.
    /// </summary>
    /// <param name="startDate">The start date for the new operating model.</param>
    /// <param name="methodology">The methodology the team uses.</param>
    /// <param name="sizingMethod">The sizing method the team uses.</param>
    /// <returns>A result containing the new operating model or an error.</returns>
    public Result<TeamOperatingModel> SetOperatingModel(LocalDate startDate, Methodology methodology, SizingMethod sizingMethod)
    {
        var currentModel = _operatingModels.SingleOrDefault(m => m.IsCurrent);

        var result = TeamOperatingModel.Create(startDate, methodology, sizingMethod, currentModel);

        if (result.IsSuccess)
        {
            _operatingModels.Add(result.Value);
        }

        return result;
    }

    /// <summary>
    /// Removes the current operating model from the team.
    /// A team must always have at least one operating model, so the current model
    /// can only be removed if there is at least one historical model to fall back to.
    /// </summary>
    /// <param name="operatingModelId">The operating model identifier to remove.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result RemoveOperatingModel(Guid operatingModelId)
    {
        var operatingModel = _operatingModels.SingleOrDefault(m => m.Id == operatingModelId);

        if (operatingModel is null)
        {
            return Result.Failure($"Operating model with Id {operatingModelId} not found for this team.");
        }

        if (!operatingModel.IsCurrent)
        {
            return Result.Failure("Only the current operating model can be removed. Historical operating models must be preserved to maintain data integrity.");
        }

        if (_operatingModels.Count == 1)
        {
            return Result.Failure("Cannot remove the last operating model. A team must always have at least one operating model.");
        }

        // Find the previous operating model (the one with the latest start date before the current one)
        var previousModel = _operatingModels
            .Where(m => m.Id != operatingModelId)
            .OrderByDescending(m => m.DateRange.Start)
            .First();

        // Clear the end date to make it current again
        previousModel.ClearEndDate();

        _operatingModels.Remove(operatingModel);

        return Result.Success();
    }

    /// <summary>Creates the specified team with an initial operating model.</summary>
    /// <param name="name">The name.</param>
    /// <param name="code">The code.</param>
    /// <param name="description">The description.</param>
    /// <param name="activeDate">The active date.</param>
    /// <param name="methodology">The initial methodology for the team's operating model.</param>
    /// <param name="sizingMethod">The initial sizing method for the team's operating model.</param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns>The new team.</returns>
    public static Team Create(string name, TeamCode code, string? description, LocalDate activeDate, Methodology methodology, SizingMethod sizingMethod, EventActor actor, Instant timestamp)
    {
        var team = new Team(name, code, description, activeDate);

        team.AddPostPersistenceAction(() =>
            team.AddDomainEvent(new TeamCreatedEvent(team.Id, team.Key, team.Code, team.Name, team.Description, team.Type, team.ActiveDate, team.InactiveDate, team.IsActive, actor, timestamp))
        );

        team.SetOperatingModel(activeDate, methodology, sizingMethod);

        return team;
    }
}
