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
/// A versioned cut of one releasable product node. May ship on its own or inside a
/// <see cref="ReleasePackage"/>.
/// </summary>
/// <remarks>
/// Describes what was cut, never where it went — rollout lives on <see cref="Deployment"/>. A release
/// with no deployment is a complete record, which is what makes release-first hand-entry workable.
/// </remarks>
public sealed class Release : StatusTrackedEntity, IHasIdAndKey
{
    private Release(){ }

    private Release(Guid productId, string version, string? name, LocalDate? targetDate, long? sequence)
    {
        ProductId = productId;
        Version = version;
        Name = name;
        TargetDate = targetDate;
        Sequence = sequence;
    }

    /// <inheritdoc/>
    public override string StatusOwnerType => ProductWorkflowOwners.Release.Key;

    /// <summary>
    /// The unique auto-generated key of the release. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The product node this release was cut against.
    /// </summary>
    public Guid ProductId { get; private init; }

    /// <summary>
    /// The product this release was cut against, when one is loaded.
    /// </summary>
    /// <remarks>
    /// For the read side only. Domain methods take the product name they need as an argument, so no
    /// invariant depends on this being loaded.
    /// </remarks>
    public Product? Product { get; private init; }

    /// <summary>
    /// The version as the organization writes it — <c>4.8.2</c>, <c>2026.08</c>, <c>v3-beta</c>,
    /// a build number, a git tag.
    /// </summary>
    /// <remarks>
    /// <strong>Free text, never parsed.</strong> Nothing may compare, sort, or extract meaning from this
    /// string; ordering comes from <see cref="ReleasedDate"/>, then <see cref="Sequence"/>.
    /// </remarks>
    public string Version
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Version)).Trim();
    } = default!;

    /// <summary>
    /// An optional human name for the release, where a team gives one.
    /// </summary>
    public string? Name
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// A manual ordering override, used only where chronology misleads.
    /// </summary>
    /// <remarks>
    /// Normally null; releases order by date. Exists for backports, where <c>4.7.5</c> shipping after
    /// <c>5.0.0</c> reads as newest by date. User-supplied, unlike <c>ProjectScore.Sequence</c>.
    /// </remarks>
    public long? Sequence { get; private set; }

    /// <summary>
    /// When the release is expected to ship.
    /// </summary>
    public LocalDate? TargetDate { get; private set; }

    /// <summary>
    /// When scope was frozen. Set by <see cref="Cut"/>.
    /// </summary>
    public LocalDate? CutDate { get; private set; }

    /// <summary>
    /// When it actually shipped. Set by <see cref="MarkReleased"/>; the basis for release frequency.
    /// </summary>
    public LocalDate? ReleasedDate { get; private set; }

    /// <summary>
    /// Notes for this release, authored by hand or generated.
    /// </summary>
    public string? Notes
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The package this release shipped inside, or <c>null</c> when it shipped on its own.
    /// </summary>
    /// <remarks>
    /// Nothing writes this yet: <see cref="SetPackage"/> is the only path to it and has no caller, so
    /// in practice it is always null and a package's membership is read from its manifest instead.
    /// </remarks>
    public Guid? PackageId { get; private set; }

    /// <summary>
    /// The package this release shipped inside, when one is loaded.
    /// </summary>
    public ReleasePackage? Package { get; private init; }


    /// <summary>
    /// Updates the version, name, notes or ordering sequence.
    /// </summary>
    /// <remarks>
    /// Raises nothing when every value already matches. Compares trimmed input because the setters trim.
    /// </remarks>
    public Result UpdateDetails(string version, string? name, string? notes, long? sequence, EventActor actor, Instant timestamp)
    {
        var newVersion = Guard.Against.NullOrWhiteSpace(version, nameof(version)).Trim();
        var newName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var newNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (string.Equals(Version, newVersion, StringComparison.Ordinal)
            && string.Equals(Name, newName, StringComparison.Ordinal)
            && string.Equals(Notes, newNotes, StringComparison.Ordinal)
            && Sequence == sequence)
        {
            return Result.Success();
        }

        Version = newVersion;
        Name = newName;
        Notes = newNotes;
        Sequence = sequence;

        AddDomainEvent(new ReleaseDetailsUpdatedEvent(Id, Key, ProductId, Version, Name, Sequence, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves the target date.
    /// </summary>
    public Result MoveTargetDate(LocalDate? targetDate, string productName, EventActor actor, Instant timestamp)
    {
        if (StatusCategory is StatusCategory.Done or StatusCategory.Removed)
        {
            return Result.Failure("A released or withdrawn release cannot have its target date moved.");
        }

        if (targetDate == TargetDate)
        {
            return Result.Success();
        }

        var fromTargetDate = TargetDate;
        TargetDate = targetDate;

        AddDomainEvent(new ReleaseTargetDateMovedEvent(Id, Key, ProductId, productName, Version, fromTargetDate, targetDate, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Freezes scope and marks the release ready to ship.
    /// </summary>
    /// <param name="readyStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Ready"/>, resolved by the caller.
    /// </param>
    public Result Cut(LocalDate cutDate, StatusRef readyStatus, string productName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(readyStatus, nameof(readyStatus));

        if (CutDate is not null)
        {
            return Result.Failure("This release has already been cut.");
        }

        if (StatusCategory is StatusCategory.Done or StatusCategory.Removed)
        {
            return Result.Failure("A released or withdrawn release cannot be cut.");
        }

        CutDate = cutDate;
        ApplyStatus(readyStatus, actor, timestamp);

        AddDomainEvent(new ReleaseCutEvent(Id, Key, ProductId, productName, Version, cutDate, StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the release shipped.
    /// </summary>
    /// <param name="releasedStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Released"/>, resolved by the caller.
    /// </param>
    /// <remarks>Named <c>MarkReleased</c> because C# forbids a member matching its type's name.</remarks>
    public Result MarkReleased(LocalDate releasedDate, StatusRef releasedStatus, string productName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(releasedStatus, nameof(releasedStatus));

        if (ReleasedDate is not null)
        {
            return Result.Failure("This release has already been released.");
        }

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("A withdrawn release cannot be released.");
        }

        if (CutDate is not null && releasedDate < CutDate)
        {
            return Result.Failure("The released date cannot be before the cut date.");
        }

        ReleasedDate = releasedDate;
        ApplyStatus(releasedStatus, actor, timestamp);

        AddDomainEvent(new ReleaseReleasedEvent(Id, Key, ProductId, productName, Version, releasedDate, StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Pulls the release after it was cut. Phase one's failure proxy.
    /// </summary>
    /// <param name="withdrawnStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Withdrawn"/>, resolved by the caller.
    /// </param>
    public Result Withdraw(string? reason, StatusRef withdrawnStatus, string productName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(withdrawnStatus, nameof(withdrawnStatus));

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("This release has already been withdrawn.");
        }

        ApplyStatus(withdrawnStatus, actor, timestamp, reason);

        AddDomainEvent(new ReleaseWithdrawnEvent(Id, Key, ProductId, productName, Version, reason?.Trim(), StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that this release ships inside a package, or on its own again.
    /// </summary>
    internal void SetPackage(Guid? packageId) => PackageId = packageId;

    /// <summary>
    /// Creates a release against a product node.
    /// </summary>
    /// <param name="isProductReleasable">
    /// Whether the product's type permits releases. Supplied by the caller, which owns the type lookup.
    /// </param>
    public static Result<Release> Create(
        Guid productId,
        string version,
        string? name,
        LocalDate? targetDate,
        long? sequence,
        bool isProductReleasable,
        StatusRef initialStatus,
        string productName,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Default(productId, nameof(productId));
        Guard.Against.Null(initialStatus, nameof(initialStatus));

        if (!isProductReleasable)
        {
            return Result.Failure<Release>("Releases cannot be cut against this product's type.");
        }

        var release = new Release(productId, version, name, targetDate, sequence);
        release.ApplyStatus(initialStatus, actor, timestamp);

        // Deferred because Key is database-generated: an event raised here would carry Key 0.
        release.AddPostPersistenceAction(() => release.AddDomainEvent(new ReleasePlannedEvent(
            release.Id,
            release.Key,
            release.ProductId,
            productName,
            release.Version,
            release.Name,
            release.TargetDate,
            release.StatusId,
            release.StatusCategory,
            actor,
            timestamp)));

        return Result.Success(release);
    }
}
