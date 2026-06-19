using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// A copy of the Wayd.Common.Domain.Interfaces.Organization.ISimpleTeam interface.  Used to hold basic team information for the project portfolio management service and db context.
/// </summary>
public sealed class PpmTeam : ISimpleTeam, IHasIdAndKey, IHasTeamIdAndCode
{
    private PpmTeam() { }

    public PpmTeam(TeamCreatedEvent team)
    {
        Id = team.Id;
        Key = team.Key;
        Name = team.Name;
        Code = team.Code;
        Type = team.Type;
        IsActive = team.IsActive;
    }

    public PpmTeam(ISimpleTeam team)
    {
        Id = team.Id;
        Key = team.Key;
        Name = team.Name;
        Code = team.Code;
        Type = team.Type;
        IsActive = team.IsActive;
    }

    public Guid Id { get; private set; }
    public int Key { get; private set; }
    public string Name { get; private set; } = default!;
    public TeamCode Code { get; private set; } = default!;
    public TeamType Type { get; private set; } = default!;
    public bool IsActive { get; private set; }

    /// <summary>
    /// Update the team information based on a TeamUpdatedEvent
    /// </summary>
    /// <param name="team"></param>
    public void Update(TeamUpdatedEvent team)
    {
        Name = team.Name;
        Code = team.Code;
    }

    public void UpdateIsActive(bool isActive)
    {
        IsActive = isActive;
    }


    /// <summary>
    /// Used to resynchronize the PpmTeam with an ISimpleTeam instance.
    /// </summary>
    /// <param name="team"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void UpdateSimpleTeam(ISimpleTeam team)
    {
        if (Id != team.Id)
        {
            throw new InvalidOperationException("Cannot update PpmTeam with a different Id.");
        }

        Key = team.Key;
        Name = team.Name;
        Code = team.Code;
        Type = team.Type;
        IsActive = team.IsActive;
    }

    public bool EqualsSimpleTeam(ISimpleTeam? other)
    {
        if (other is null) return false;

        return Id == other.Id
            && Key == other.Key
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && Equals(Code, other.Code)
            && Equals(Type, other.Type)
            && IsActive == other.IsActive;
    }
}
