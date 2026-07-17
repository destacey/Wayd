using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Work;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// This object ensures the integrity and system state of global work type levels.
/// </summary>
public sealed class WorkTypeHierarchy : BaseAuditableEntity<int>
{
    private readonly List<WorkTypeLevel> _levels = [];

    private WorkTypeHierarchy() { }

    private WorkTypeHierarchy(Instant timestamp)
    {
        _levels.AddRange(GetSystemDefaultWorkTypeLevels(timestamp));
    }

    /// <summary>
    /// List of child work type levels.
    /// </summary>
    public IReadOnlyList<WorkTypeLevel> Levels => _levels.AsReadOnly();

    /// <summary>
    /// Add a work type level to the scheme.  The work type level category must be a Portfolio Tier.
    /// </summary>
    /// <param name="workTypeLevel"></param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result<WorkTypeLevel> AddPortfolioWorkTypeLevel(string name, string? description, Instant timestamp)
    {
        Guard.Against.NullOrWhiteSpace(name);

        if (_levels.Any(l => l.Name == name.Trim()))
            return Result.Failure<WorkTypeLevel>($"A work type level with the name '{name}' already exists.");

        var maxOrder = _levels.Where(l => l.Tier == WorkTypeTier.Portfolio).Max(l => l.Order);

        var level = WorkTypeLevel.Create(name, description, WorkTypeTier.Portfolio, Ownership.Owned, maxOrder + 1, timestamp);

        _levels.Add(level);
        return Result.Success(level);
    }

    /// <summary>
    /// Update an existing work type level.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result UpdateWorkTypeLevel(int id,
            string name,
            string? description,
            Instant timestamp)
    {
        var workTypeLevel = _levels.FirstOrDefault(x => x.Id == id);
        if (workTypeLevel is null)
            return Result.Failure<int>("Work Type Level not found.");

        if (_levels.Where(l => l.Id != id).Any(l => l.Name == name.Trim()))
            return Result.Failure($"A work type level with the name '{name}' already exists.");

        var result = workTypeLevel.Update(name, description, timestamp);
        if (result.IsFailure)
            return Result.Failure<int>(result.Error);

        return Result.Success();
    }

    public Result UpdatePortfolioTierLevelsOrder(Dictionary<int, int> updatedLevels)
    {
        foreach (var (id, order) in updatedLevels)
        {
            var level = _levels.Where(l => l.Tier == WorkTypeTier.Portfolio).SingleOrDefault(l => l.Id == id);
            if (level is null)
                return Result.Failure($"Portfolio tier work type level with id {id} not found.");

            level.UpdateOrder(order);
        }

        // verify no duplicate orders
        var duplicateOrders = _levels.Where(l => l.Tier == WorkTypeTier.Portfolio)
            .GroupBy(l => l.Order)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        return duplicateOrders.Count == 0
            ? Result.Success()
            : Result.Failure("Unable to save because it would create duplicate ordering positions. Each work type level must have a unique position within the tier.");
    }

    /// <summary>Initializes the global work type hierarchy.  This should only ever called once.</summary>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public static WorkTypeHierarchy Initialize(Instant timestamp)
    {
        WorkTypeHierarchy scheme = new(timestamp);
        return scheme;
    }
    public Result Reinitialize(Instant timestamp)
    {
        try
        {
            bool hasChanged = false;
            List<WorkTypeLevel> defaultWorkTypeLevels = GetSystemDefaultWorkTypeLevels(timestamp);

            // Ensure each tier has a system owned work type level
            foreach (var tier in Enum.GetValues<WorkTypeTier>())
            {
                if (!_levels.Any(x => x.Tier == tier && x.Ownership == Ownership.System))
                {
                    foreach (var defaultWorkTypeLevel in defaultWorkTypeLevels)
                    {
                        if (defaultWorkTypeLevel.Tier == tier && defaultWorkTypeLevel.Ownership == Ownership.System)
                        {
                            _levels.Add(defaultWorkTypeLevel);
                            hasChanged = true;
                        }
                    }
                }
            }

            if (hasChanged)
            {
                // TODO: raise a concrete "hierarchy changed" domain event here once a real
                // subscriber exists (the generic entity events were removed; see
                // docs/contributing/specs/mediatr-to-wolverine-migration.md, Phase 0).
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    private static List<WorkTypeLevel> GetSystemDefaultWorkTypeLevels(Instant timestamp)
    {
        // work type levels cannot be removed or have their name changed in this list without changes to the current ReInitialize process
        List<WorkTypeLevel> levels =
        [
            WorkTypeLevel.Create("Stories", "Stories work type level.", WorkTypeTier.Requirement, Ownership.System, 1, timestamp),

            WorkTypeLevel.Create("Tasks", "Tasks work type level.", WorkTypeTier.Task, Ownership.System, 1, timestamp),

            WorkTypeLevel.Create("Other", "Work type level for work types not mapped to one of the primary work type levels.", WorkTypeTier.Other, Ownership.System, 1, timestamp),

            WorkTypeLevel.Create("Epics", "Epics work type level.", WorkTypeTier.Portfolio, Ownership.System, 1, timestamp),
            WorkTypeLevel.Create("Features", "Features work type level.", WorkTypeTier.Portfolio, Ownership.System, 2, timestamp)
        ];

        return levels;
    }
}
