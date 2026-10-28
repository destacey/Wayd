using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// A named, ordered scale of severity levels incidents may use.
/// </summary>
public sealed class IncidentSeveritySet : BaseAuditableEntity
{
    private readonly List<IncidentSeverityLevel> _levels = [];

    private IncidentSeveritySet() { }

    private IncidentSeveritySet(string name, string? description, bool isSystem)
    {
        Name = name;
        Description = description;
        IsSystem = isSystem;
        IsActive = true;
    }

    /// <summary>What the organization calls this scale.</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>Optional explanation shown on the settings page.</summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Whether Wayd seeded this scale. System scales cannot be renamed, edited, or deleted.
    /// </summary>
    public bool IsSystem { get; private init; }

    /// <summary>
    /// Whether the scale appears in the picker when creating an incident.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>Ordered levels, most severe first.</summary>
    public IReadOnlyList<IncidentSeverityLevel> Levels => _levels.AsReadOnly();

    /// <summary>Creates a custom scale with at least two levels.</summary>
    public static Result<IncidentSeveritySet> CreateCustom(
        string name,
        string? description,
        IReadOnlyList<string> levelNames)
    {
        if (levelNames.Count < 2)
        {
            return Result.Failure<IncidentSeveritySet>("A severity set must have at least two levels.");
        }

        try
        {
            var set = new IncidentSeveritySet(name, description, isSystem: false);
            set.AddLevelsFromNames(levelNames);
            return Result.Success(set);
        }
        catch (Exception ex)
        {
            return Result.Failure<IncidentSeveritySet>(ex.Message);
        }
    }

    /// <summary>Creates a system scale at seed time.</summary>
    public static Result<IncidentSeveritySet> CreateSystem(
        string name,
        string? description,
        IReadOnlyList<string> levelNames)
    {
        if (levelNames.Count < 2)
        {
            return Result.Failure<IncidentSeveritySet>("A severity set must have at least two levels.");
        }

        var set = new IncidentSeveritySet(name, description, isSystem: true);
        set.AddLevelsFromNames(levelNames);
        return Result.Success(set);
    }

    /// <summary>Renames a custom scale and updates its description.</summary>
    public Result UpdateDetails(string name, string? description)
    {
        if (IsSystem)
        {
            return Result.Failure("System severity sets cannot be edited.");
        }

        try
        {
            Name = name;
            Description = description;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Replaces level names and order on a custom scale.
    /// </summary>
    /// <param name="levels">
    /// Ordered level payloads. An existing id keeps that level; a null id adds a level.
    /// Levels missing from the list are removed when not in <paramref name="levelIdsInUse"/>.
    /// </param>
    public Result ReplaceLevels(
        IReadOnlyList<(Guid? Id, string Name)> levels,
        IReadOnlySet<Guid> levelIdsInUse)
    {
        if (IsSystem)
        {
            return Result.Failure("System severity sets cannot be edited.");
        }

        if (levels.Count < 2)
        {
            return Result.Failure("A severity set must have at least two levels.");
        }

        var names = levels.Select(l => l.Name.Trim()).ToList();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
        {
            return Result.Failure("Level names must be unique within a severity set.");
        }

        var existingById = _levels.ToDictionary(l => l.Id);
        var requestedIds = new HashSet<Guid>();

        for (var i = 0; i < levels.Count; i++)
        {
            var (id, name) = levels[i];
            if (id is null)
            {
                var added = new IncidentSeverityLevel(Id, name, i);
                _levels.Add(added);
                requestedIds.Add(added.Id);
                continue;
            }

            if (!existingById.TryGetValue(id.Value, out var level))
            {
                return Result.Failure($"Level '{id}' does not belong to this severity set.");
            }

            level.Rename(name);
            level.SetSortOrder(i);
            requestedIds.Add(id.Value);
        }

        foreach (var removed in _levels.Where(l => !requestedIds.Contains(l.Id)).ToList())
        {
            if (levelIdsInUse.Contains(removed.Id))
            {
                return Result.Failure(
                    $"Cannot remove level '{removed.Name}' while an incident still uses it.");
            }

            _levels.Remove(removed);
        }

        return Result.Success();
    }

    /// <summary>Deactivates a custom scale that is not the organization default.</summary>
    public Result Deactivate(bool isSelected)
    {
        if (IsSystem)
        {
            return Result.Failure("System severity sets cannot be deactivated.");
        }

        if (isSelected)
        {
            return Result.Failure("The selected severity set cannot be deactivated.");
        }

        if (!IsActive)
        {
            return Result.Failure("This severity set is already inactive.");
        }

        IsActive = false;
        return Result.Success();
    }

    /// <summary>Reactivates a custom scale.</summary>
    public Result Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            return Result.Success();
        }

        return Result.Failure("This severity set is already active.");
    }

    /// <summary>Removes a custom scale that is not selected.</summary>
    public Result Delete(bool isSelected, IReadOnlySet<Guid> levelIdsInUse)
    {
        if (IsSystem)
        {
            return Result.Failure("System severity sets cannot be deleted.");
        }

        if (isSelected)
        {
            return Result.Failure("The selected severity set cannot be deleted.");
        }

        if (levelIdsInUse.Count > 0 && _levels.Any(l => levelIdsInUse.Contains(l.Id)))
        {
            return Result.Failure("Cannot delete a severity set while an incident still uses one of its levels.");
        }

        return Result.Success();
    }

    private void AddLevelsFromNames(IReadOnlyList<string> levelNames)
    {
        for (var i = 0; i < levelNames.Count; i++)
        {
            _levels.Add(new IncidentSeverityLevel(Id, levelNames[i], i));
        }
    }
}
