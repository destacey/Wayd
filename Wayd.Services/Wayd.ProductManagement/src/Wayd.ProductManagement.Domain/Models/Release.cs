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
    /// Records that a release marked as shipped did not in fact ship, moving it back.
    /// </summary>
    /// <param name="toStatus">
    /// The status to return to: the one aliased <see cref="ProductStatusAlias.Ready"/> where the
    /// release was cut, otherwise its workflow's initial status. Resolved by the caller.
    /// </param>
    /// <remarks>
    /// This is not a withdrawal. Withdrawing says a real release was pulled; reverting says the
    /// release never happened and the record was wrong. Recording the first as the second leaves an
    /// append-only history asserting a withdrawal nobody performed, which is exactly what a reader
    /// later relies on being true.
    /// <para>
    /// The released date goes with the status, because the two are one fact. A reason is required:
    /// unlike a date correction, this contradicts something the history already asserts, so the record
    /// has to say why.
    /// </para>
    /// </remarks>
    public Result RevertRelease(StatusRef toStatus, string reason, string productName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(toStatus, nameof(toStatus));

        if (ReleasedDate is null)
        {
            return Result.Failure("This release has not been released, so there is nothing to revert.");
        }

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("A withdrawn release cannot be reverted. Its status is already terminal.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure("A reason is required to revert a release.");
        }

        var fromReleasedDate = ReleasedDate;
        ReleasedDate = null;
        ApplyStatus(toStatus, actor, timestamp, reason);

        AddDomainEvent(new ReleaseRevertedEvent(
            Id, Key, ProductId, productName, Version, fromReleasedDate.Value, reason.Trim(), StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Corrects the recorded target, cut and released dates without moving the release's status.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Cut"/> and <see cref="MarkReleased"/> assert that something happened, so each sets a
    /// date and applies a status together and refuses to run twice. Neither can fix a date entered
    /// wrongly. Without this, the only route to a corrected released date is to withdraw the release
    /// and release it again, which writes two status transitions that never happened into an
    /// append-only history.
    /// </para>
    /// <para>
    /// A correction says what was written down was wrong, not that the release moved, so status is
    /// left alone. Dates may be added as well as changed: a release can be marked released without
    /// ever being cut — historical import depends on it — so a cut date discovered later is a
    /// correction, not a lifecycle step.
    /// </para>
    /// <para>
    /// The target and cut dates may also be cleared, because each is only a record of something
    /// written down. The released date is the exception: emptying it on a released record would leave
    /// the status contradicting the dates. Recording that a release did not in fact ship is
    /// <see cref="RevertRelease"/>'s job, which moves the status to match.
    /// </para>
    /// <para>
    /// The one ordering rule that survives is real: a release cannot ship before it was cut.
    /// </para>
    /// </remarks>
    public Result CorrectDates(
        LocalDate? targetDate,
        LocalDate? cutDate,
        LocalDate? releasedDate,
        string productName,
        EventActor actor,
        Instant timestamp)
    {
        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("A withdrawn release cannot have its dates corrected.");
        }


        if (releasedDate is null && ReleasedDate is not null)
        {
            return Result.Failure(
                "A released release cannot have its released date removed. Revert the release instead.");
        }

        if (cutDate is not null && releasedDate is not null && releasedDate < cutDate)
        {
            return Result.Failure("The released date cannot be before the cut date.");
        }

        if (targetDate == TargetDate && cutDate == CutDate && releasedDate == ReleasedDate)
        {
            return Result.Success();
        }

        var fromTargetDate = TargetDate;
        var fromCutDate = CutDate;
        var fromReleasedDate = ReleasedDate;
        TargetDate = targetDate;
        CutDate = cutDate;
        ReleasedDate = releasedDate;

        AddDomainEvent(new ReleaseDatesCorrectedEvent(
            Id, Key, ProductId, productName, Version,
            fromTargetDate, targetDate, fromCutDate, cutDate, fromReleasedDate, releasedDate, actor, timestamp));

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
