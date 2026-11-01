using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Common.Domain.Interfaces;
using NodaTime;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// A named set of values a team votes with, such as Fibonacci or t-shirt sizes. Its values live on
/// published <see cref="EstimationScaleVersion"/>s: changing them publishes the next version, and whatever
/// uses the scale stays on the version it chose.
/// </summary>
public sealed class EstimationScale : BaseAuditableEntity, IHasIdAndKey
{
    /// <summary>
    /// The fewest estimate values a version may have: one size alone is no scale.
    /// </summary>
    public const int MinimumEstimateValues = 2;

    private readonly List<EstimationScaleVersion> _versions = [];

    private EstimationScale() { }

    private EstimationScale(string name, string? description)
    {
        Name = name;
        Description = description;
        IsActive = true;
    }

    /// <summary>
    /// The unique key of the Estimation Scale. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the Estimation Scale.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The description of the Estimation Scale.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// Whether the estimation scale is active and available to new poker sessions.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Every version the scale has published, oldest first once ordered by <see cref="EstimationScaleVersion.Number"/>.
    /// </summary>
    public IReadOnlyCollection<EstimationScaleVersion> Versions => _versions.AsReadOnly();

    /// <summary>
    /// The latest published version, which anything newly choosing the scale is pinned to.
    /// </summary>
    /// <exception cref="InvalidOperationException">The versions were not loaded.</exception>
    public EstimationScaleVersion CurrentVersion => _versions.Count > 0
        ? _versions.MaxBy(v => v.Number)!
        : throw new InvalidOperationException($"The versions of estimation scale {Id} were not loaded.");

    /// <summary>
    /// Rename the scale or edit its description. Neither changes what a value means, so no version is published.
    /// </summary>
    public Result UpdateDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        try
        {
            // Compared after assignment, never against the arguments: the setters trim.
            var before = new EstimationScaleDetails(Name, Description);

            Name = name;
            Description = description;

            var after = new EstimationScaleDetails(Name, Description);
            if (before != after)
                AddKeyedDomainEvent(() => new EstimationScaleDetailsUpdatedEvent(Id, Key, after.Name, after.Description, before, actor, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Publish <paramref name="values"/> as the scale's next version. Values identical to the current
    /// version's, flags and order included, publish nothing.
    /// </summary>
    public Result PublishVersion(IEnumerable<EstimationScaleValue> values, EventActor actor, Instant timestamp)
    {
        var valueList = values.ToList();
        var validation = Validate(valueList);
        if (validation.IsFailure)
            return validation;

        var previous = CurrentVersion;
        if (previous.HasValues(valueList))
            return Result.Success();

        var version = new EstimationScaleVersion(Id, previous.Number + 1, valueList, timestamp);
        _versions.Add(version);

        var versionValues = ToValues(version);
        var (previousId, previousNumber) = (previous.Id, previous.Number);
        AddKeyedDomainEvent(() => new EstimationScaleVersionPublishedEvent(Id, Key, versionValues, previousId, previousNumber, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Activate the estimation scale.
    /// </summary>
    public Result Activate(EventActor actor, Instant timestamp)
    {
        if (IsActive)
            return Result.Failure("The estimation scale is already active.");

        IsActive = true;
        AddKeyedDomainEvent(() => new EstimationScaleActivatedEvent(Id, Key, actor, timestamp));
        return Result.Success();
    }

    /// <summary>
    /// Deactivate the estimation scale. Nothing new can choose it; what already uses it keeps its version.
    /// </summary>
    public Result Deactivate(EventActor actor, Instant timestamp)
    {
        if (!IsActive)
            return Result.Failure("The estimation scale is already inactive.");

        IsActive = false;
        AddKeyedDomainEvent(() => new EstimationScaleDeactivatedEvent(Id, Key, actor, timestamp));
        return Result.Success();
    }

    /// <summary>
    /// Raise the deletion event. The caller removes the scale in the same save, after checking nothing uses it.
    /// </summary>
    public void Delete(EventActor actor, Instant timestamp)
    {
        AddDomainEvent(new EstimationScaleDeletedEvent(Id, Key, Name, actor, timestamp));
    }

    /// <summary>
    /// Create a new, active estimation scale with <paramref name="values"/> as its first version.
    /// </summary>
    public static Result<EstimationScale> Create(string name, string? description, IEnumerable<EstimationScaleValue> values, EventActor actor, Instant timestamp)
    {
        var valueList = values.ToList();
        var validation = Validate(valueList);
        if (validation.IsFailure)
            return Result.Failure<EstimationScale>(validation.Error);

        try
        {
            var scale = new EstimationScale(name, description);
            var version = new EstimationScaleVersion(scale.Id, 1, valueList, timestamp);
            scale._versions.Add(version);

            var (createdName, createdDescription, isActive) = (scale.Name, scale.Description, scale.IsActive);
            var versionValues = ToValues(version);
            scale.AddPostPersistenceAction(() => scale.AddDomainEvent(new EstimationScaleCreatedEvent(
                scale.Id, scale.Key, createdName, createdDescription, isActive, versionValues, actor, timestamp)));

            return Result.Success(scale);
        }
        catch (Exception ex)
        {
            return Result.Failure<EstimationScale>(ex.Message);
        }
    }

    private static Result Validate(IReadOnlyList<EstimationScaleValue> values)
    {
        if (values.Count(v => v.IsEstimate) < MinimumEstimateValues)
            return Result.Failure($"An estimation scale must have at least {MinimumEstimateValues} estimate values.");

        var duplicate = values
            .GroupBy(v => v.Value, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            return Result.Failure($"The value '{duplicate.Key}' appears more than once.");

        return Result.Success();
    }

    private static EstimationScaleVersionValues ToValues(EstimationScaleVersion version) => new(
        version.Id,
        version.Number,
        [.. version.Values.Select(v => new EstimationScaleValueValues(v.Value, v.IsEstimate))]);

    /// <summary>
    /// Raises an event that carries <see cref="Key"/>, waiting for the first save to assign it.
    /// </summary>
    /// <remarks>
    /// The factory runs when the event is raised, so everything else it carries must be captured in locals by
    /// the caller — only Key may be read inside it.
    /// </remarks>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }
}
