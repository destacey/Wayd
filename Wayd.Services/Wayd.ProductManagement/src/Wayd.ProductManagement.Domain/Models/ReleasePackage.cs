using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// Several component releases shipped as one coordinated unit — the weekly pipeline run that deploys
/// fifteen services together. Versioned in its own right, and carrying a manifest of every component
/// version it shipped.
/// </summary>
/// <remarks>
/// Never call this a Release Train — that is SAFe's organizational construct, already modelled as
/// <c>TeamOfTeams</c>. Where a package exists it owns the deployment and its component releases do not,
/// so one pipeline run counts once.
/// </remarks>
public sealed class ReleasePackage : StatusTrackedEntity, IHasIdAndKey
{
    private readonly List<ReleasePackageComponent> _components = [];

    private ReleasePackage() { }

    private ReleasePackage(string version, string? name, LocalDate? targetDate, StatusRef status)
    {
        Version = version;
        Name = name;
        TargetDate = targetDate;
    }

    /// <inheritdoc/>
    public override string StatusOwnerType => ProductWorkflowOwners.ReleasePackage.Key;

    /// <summary>
    /// The unique auto-generated key of the package. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The package's own version, distinct from any component's. Free text, never parsed.
    /// </summary>
    public string Version
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Version)).Trim();
    } = default!;

    /// <summary>An optional human name for the package.</summary>
    public string? Name
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>When the package is expected to ship.</summary>
    public LocalDate? TargetDate { get; private set; }

    /// <summary>When the package shipped.</summary>
    public LocalDate? ReleasedDate { get; private set; }

    /// <summary>
    /// Every component version this package shipped — changed and carried forward alike.
    /// </summary>
    public IReadOnlyCollection<ReleasePackageComponent> Components => _components.AsReadOnly();

    /// <summary>
    /// The components that actually changed in this package.
    /// </summary>
    public IReadOnlyCollection<ReleasePackageComponent> ChangedComponents =>
        _components.Where(c => c.Kind == ManifestEntryKind.Changed).ToList().AsReadOnly();

    /// <summary>
    /// Replaces the manifest wholesale.
    /// </summary>
    /// <remarks>
    /// Whole-manifest replacement, not incremental: a partially-updated manifest would claim a set of
    /// versions that never shipped together.
    /// </remarks>
    public Result SetManifest(IReadOnlyCollection<(Guid ProductId, Guid? ReleaseId, string Version, ManifestEntryKind Kind)> components, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(components, nameof(components));

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("A withdrawn package's manifest cannot be amended.");
        }

        // Once shipped, the manifest is the record of what went out rather than a plan. Rewriting it
        // would claim a set of versions that never shipped together — the exact failure the
        // whole-manifest rule above exists to prevent.
        if (ReleasedDate is not null)
        {
            return Result.Failure("A released package's manifest cannot be amended.");
        }

        if (components.Count == 0)
        {
            return Result.Failure("A package manifest cannot be empty.");
        }

        var duplicate = components.GroupBy(c => c.ProductId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return Result.Failure("A component can appear only once in a package manifest.");
        }

        // Order is not part of the comparison: a manifest is a set.
        if (MatchesManifest(components))
        {
            return Result.Success();
        }

        _components.Clear();
        foreach (var component in components)
        {
            _components.Add(new ReleasePackageComponent(Id, component.ProductId, component.ReleaseId, component.Version, component.Kind));
        }

        AddDomainEvent(new PackageManifestAmendedEvent(Id, Key, Version, _components.Count, ChangedComponents.Count, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Whether the supplied components are exactly the manifest this package already holds.
    /// </summary>
    private bool MatchesManifest(IReadOnlyCollection<(Guid ProductId, Guid? ReleaseId, string Version, ManifestEntryKind Kind)> components)
    {
        if (components.Count != _components.Count)
        {
            return false;
        }

        var existing = _components.ToDictionary(c => c.ProductId);

        return components.All(c =>
            existing.TryGetValue(c.ProductId, out var match)
            && match.ReleaseId == c.ReleaseId
            && string.Equals(match.Version, c.Version?.Trim(), StringComparison.Ordinal)
            && match.Kind == c.Kind);
    }

    /// <summary>
    /// Records that the package shipped.
    /// </summary>
    public Result MarkReleased(LocalDate releasedDate, StatusRef releasedStatus, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(releasedStatus, nameof(releasedStatus));

        if (ReleasedDate is not null)
        {
            return Result.Failure("This package has already been released.");
        }

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("A withdrawn package cannot be released.");
        }

        if (_components.Count == 0)
        {
            return Result.Failure("A package cannot be released with an empty manifest.");
        }

        ReleasedDate = releasedDate;
        ApplyStatus(releasedStatus, actor, timestamp);

        AddDomainEvent(new PackageReleasedEvent(Id, Key, Version, releasedDate, _components.Count, StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Pulls the package after it was assembled.
    /// </summary>
    public Result Withdraw(string? reason, StatusRef withdrawnStatus, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(withdrawnStatus, nameof(withdrawnStatus));

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("This package has already been withdrawn.");
        }

        ApplyStatus(withdrawnStatus, actor, timestamp, reason);

        AddDomainEvent(new PackageWithdrawnEvent(Id, Key, Version, reason?.Trim(), StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Assembles a package from a set of component versions.
    /// </summary>
    public static Result<ReleasePackage> Create(
        string version,
        string? name,
        LocalDate? targetDate,
        IReadOnlyCollection<(Guid ProductId, Guid? ReleaseId, string Version, ManifestEntryKind Kind)> components,
        StatusRef initialStatus,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(components, nameof(components));
        Guard.Against.Null(initialStatus, nameof(initialStatus));

        if (components.Count == 0)
        {
            return Result.Failure<ReleasePackage>("A package must be assembled from at least one component.");
        }

        var duplicate = components.GroupBy(c => c.ProductId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return Result.Failure<ReleasePackage>("A component can appear only once in a package manifest.");
        }

        var package = new ReleasePackage(version, name, targetDate, initialStatus);
        package.ApplyStatus(initialStatus, actor, timestamp);

        foreach (var component in components)
        {
            package._components.Add(new ReleasePackageComponent(package.Id, component.ProductId, component.ReleaseId, component.Version, component.Kind));
        }

        // Deferred because Key is database-generated: an event raised here would carry Key 0.
        package.AddPostPersistenceAction(() => package.AddDomainEvent(new PackageAssembledEvent(
            package.Id,
            package.Key,
            package.Version,
            package.Name,
            package._components.Count,
            package.ChangedComponents.Count,
            package.StatusId,
            package.StatusCategory,
            actor,
            timestamp)));

        return Result.Success(package);
    }
}
