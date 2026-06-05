using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.Scoring;

/// <summary>
/// A named, reusable rating scale on a scoring model (e.g., "Fibonacci" = 1,2,3,5,8,13; "Impact" =
/// Very High..Very Low). A scale owns an ordered list of <see cref="ScoringRatingLevel"/> options, and
/// criteria reference a scale to constrain how they are rated. A criterion with no scale is rated by
/// free numeric entry instead.
/// </summary>
public sealed class ScoringScale : BaseAuditableEntity
{
    private readonly List<ScoringRatingLevel> _levels = [];

    private ScoringScale() { }

    internal ScoringScale(Guid scoringModelId, string name, int order)
    {
        ScoringModelId = scoringModelId;
        Name = name;
        Order = order;
    }

    /// <summary>
    /// The ID of the scoring model this scale belongs to.
    /// </summary>
    public Guid ScoringModelId { get; private init; }

    /// <summary>
    /// The name of the scale (e.g., "Fibonacci", "Impact"). Unique within the model.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The display order of the scale within the model.
    /// </summary>
    public int Order { get; internal set; }

    /// <summary>
    /// The ordered rating levels that make up this scale.
    /// </summary>
    public IReadOnlyCollection<ScoringRatingLevel> Levels => _levels.AsReadOnly();

    internal Result Update(string name)
    {
        Name = name;
        return Result.Success();
    }

    internal ScoringRatingLevel AddLevel(string label, decimal value)
    {
        var order = _levels.Count > 0 ? _levels.Max(l => l.Order) + 1 : 1;
        var level = new ScoringRatingLevel(Id, label, value, order);
        _levels.Add(level);
        return level;
    }

    internal Result UpdateLevel(Guid levelId, string label, decimal value)
    {
        var level = _levels.FirstOrDefault(l => l.Id == levelId);
        if (level is null)
        {
            return Result.Failure("Rating level not found.");
        }

        return level.Update(label, value);
    }

    internal Result RemoveLevel(Guid levelId)
    {
        var level = _levels.FirstOrDefault(l => l.Id == levelId);
        if (level is null)
        {
            return Result.Failure("Rating level not found.");
        }

        _levels.Remove(level);
        ReorderLevels();
        return Result.Success();
    }

    internal Result ReorderLevels(List<Guid> orderedLevelIds)
    {
        Guard.Against.Null(orderedLevelIds, nameof(orderedLevelIds));

        if (orderedLevelIds.Count != _levels.Count)
        {
            return Result.Failure("The number of rating level IDs must match the number of existing rating levels.");
        }

        if (orderedLevelIds.Distinct().Count() != orderedLevelIds.Count)
        {
            return Result.Failure("Duplicate rating level IDs are not allowed.");
        }

        for (int i = 0; i < orderedLevelIds.Count; i++)
        {
            var level = _levels.FirstOrDefault(l => l.Id == orderedLevelIds[i]);
            if (level is null)
            {
                return Result.Failure($"Rating level with ID '{orderedLevelIds[i]}' not found.");
            }

            level.Order = i + 1;
        }

        return Result.Success();
    }

    /// <summary>
    /// Adds a level during construction (factory use), preserving the supplied order.
    /// </summary>
    internal void SeedLevel(string label, decimal value, int order)
        => _levels.Add(new ScoringRatingLevel(Id, label, value, order));

    private void ReorderLevels()
    {
        int order = 1;
        foreach (var level in _levels.OrderBy(l => l.Order))
        {
            level.Order = order;
            order++;
        }
    }
}
