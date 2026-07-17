using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Extensions;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>Allows work types to be grouped and defined in a hierarchy.</summary>
public sealed class WorkTypeLevel : BaseAuditableEntity<int>
{
    private WorkTypeLevel() { }

    private WorkTypeLevel(string name, string? description, WorkTypeTier tier, Ownership ownership, int order)
    {
        Name = name;
        Description = description;
        Tier = tier;
        Ownership = ownership;
        Order = order;
    }

    /// <summary>The name of the work type level.</summary>
    /// <value>The name.</value>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>The description of the work type level.</summary>
    /// <value>The description.</value>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    public WorkTypeTier Tier { get; private init; }

    /// <summary>
    /// Indicates whether the work type level is owned by Wayd or a third party system.  This value can not change.
    /// </summary>
    /// <value>The ownership.</value>
    public Ownership Ownership { get; private init; }

    /// <summary>
    /// The order of the work type level.
    /// </summary>
    /// <value>The order.</value>
    public int Order { get; private set; }

    /// <summary>Updates the specified name.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    internal Result Update(string name, string? description, Instant timestamp)
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

    /// <summary>
    /// Updates the order of the work type level within the tier.
    /// </summary>
    /// <param name="order"></param>
    public void UpdateOrder(int order)
    {
        Order = order;
    }

    /// <summary>
    /// Creates a new work type level.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="tier"></param>
    /// <param name="ownership"></param>
    /// <param name="order"></param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    internal static WorkTypeLevel Create(string name, string? description, WorkTypeTier tier, Ownership ownership, int order, Instant timestamp)
    {
        WorkTypeLevel workTypeLevel = new(name, description, tier, ownership, order);
        return workTypeLevel;
    }
}