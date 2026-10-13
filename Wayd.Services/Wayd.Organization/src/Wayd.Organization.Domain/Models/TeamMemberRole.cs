using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Extensions;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// Represents a role that can be assigned to a team member, including its name, description, and active status.
/// </summary>
/// <remarks>A team member role defines the permissions or responsibilities that can be assigned to users within a
/// team. Roles can be activated or deactivated to control their availability for assignment. Instances are created and
/// updated through provided methods to ensure validation and consistency.</remarks>
public sealed class TeamMemberRole : BaseSoftDeletableEntity, IHasIdAndKey
{
    private TeamMemberRole() { }

    private TeamMemberRole(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>
    /// The unique identifier for the team member role. This is a read-only property that is set by the database upon insertion.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the team member role.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>
    /// The description of the team member role.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The active status of the team member role. Indicates whether the role is currently active and can be assigned to team members.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Whether time allocated to a team in this role adds to the team's delivery capacity. False for a role
    /// such as a manager's, whose time on the team is real but does not burn the team's planned work. Such an
    /// allocation still counts toward the employee's 100%.
    /// </summary>
    public bool CountsTowardDeliveryCapacity { get; private set; } = true;

    /// <summary>
    /// Updates the object's name and description with the specified values.
    /// </summary>
    /// <param name="name">The new name to assign. Cannot be null.</param>
    /// <param name="description">The new description to assign. May be null to clear the description.</param>
    /// <returns>A Result indicating whether the update was successful. Returns a failure Result if an exception occurs.</returns>
    public Result Update(string name, string? description)
    {
        try
        {
            Name = name;
            Description = description;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>Sets whether time allocated in this role adds to a team's delivery capacity.</summary>
    public Result SetCountsTowardDeliveryCapacity(bool countsTowardDeliveryCapacity)
    {
        CountsTowardDeliveryCapacity = countsTowardDeliveryCapacity;
        return Result.Success();
    }

    /// <summary>
    /// Activates the current instance and marks it as active.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating the outcome of the activation operation. Returns <see cref="Result.Success"/>
    /// if activation succeeds.</returns>
    public Result Activate()
    {
        IsActive = true;
        return Result.Success();
    }

    /// <summary>
    /// Deactivates the current instance.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating the outcome of the deactivation operation.</returns>
    public Result Deactivate()
    {
        IsActive = false;
        return Result.Success();
    }

    /// <summary>
    /// Creates a new instance of <see cref="TeamMemberRole"/> with the specified name and description.
    /// </summary>
    /// <param name="name">The name of the role. Cannot be null or whitespace.</param>
    /// <param name="description">An optional description of the role. Whitespace-only values are treated as null.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the new role, or a failure result if validation fails.</returns>
    public static Result<TeamMemberRole> Create(string name, string? description)
    {
        try
        {
            return Result.Success(new TeamMemberRole(name, description));
        }
        catch (Exception ex)
        {
            return Result.Failure<TeamMemberRole>(ex.ToString());
        }
    }
}
