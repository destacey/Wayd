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
/// A versioned cut of one releasable product node — <c>Wayd API 4.12.0</c>. The artifact half of
/// delivery: what was built, at what version, and when scope froze.
/// </summary>
/// <remarks>
/// Describes what was cut, never where it went — rollout lives on <see cref="Deployment"/>. A version
/// with no deployment is a complete record, which is what makes version-first hand-entry workable.
/// <para>
/// Distinct from <see cref="Release"/>, which is what was announced to customers. A version answers
/// "what version of this one artifact?"; a release answers "what did we tell customers?". One release
/// commonly spans several versions, and a version may ship without ever being announced.
/// </para>
/// </remarks>
public sealed class Version : StatusTrackedEntity, IHasIdAndKey
{
    private Version() { }

    private Version(Guid productId, string number, string? name, LocalDate? targetDate, long? sequence)
    {
        ProductId = productId;
        Number = number;
        Name = name;
        TargetDate = targetDate;
        Sequence = sequence;
    }

    /// <inheritdoc/>
    public override string StatusOwnerType => ProductWorkflowOwners.Version.Key;

    /// <summary>
    /// The unique auto-generated key of the version. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The product node this version was cut against.
    /// </summary>
    public Guid ProductId { get; private init; }

    /// <summary>
    /// The product this version was cut against, when one is loaded.
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
    /// <para>
    /// Named <c>Number</c> rather than <c>Version</c> because C# forbids a member matching its type's
    /// name. It is still a version string, not a number, and is never parsed as one.
    /// </para>
    /// </remarks>
    public string Number
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Number)).Trim();
    } = default!;

    /// <summary>
    /// An optional human name for the version, where a team gives one.
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
    /// Normally null; versions order by date. Exists for backports, where <c>4.7.5</c> shipping after
    /// <c>5.0.0</c> reads as newest by date. User-supplied, unlike <c>ProjectScore.Sequence</c>.
    /// </remarks>
    public long? Sequence { get; private set; }

    /// <summary>
    /// When the version is expected to ship.
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
    /// Engineering notes for this version, authored by hand or generated — <c>Bumped Npgsql to 9.0.2</c>.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Release.Notes"/>, which is what customers are told. The same shipment
    /// is described twice on purpose: the two audiences want different facts, and collapsing them
    /// would force one of the two to be written for the wrong reader.
    /// </remarks>
    public string? Notes
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Updates the version number, name, notes or ordering sequence.
    /// </summary>
    /// <remarks>
    /// Raises nothing when every value already matches. Compares trimmed input because the setters trim.
    /// </remarks>
    public Result UpdateDetails(string number, string? name, string? notes, long? sequence, EventActor actor, Instant timestamp)
    {
        var newNumber = Guard.Against.NullOrWhiteSpace(number, nameof(number)).Trim();
        var newName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var newNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (string.Equals(Number, newNumber, StringComparison.Ordinal)
            && string.Equals(Name, newName, StringComparison.Ordinal)
            && string.Equals(Notes, newNotes, StringComparison.Ordinal)
            && Sequence == sequence)
        {
            return Result.Success();
        }

        Number = newNumber;
        Name = newName;
        Notes = newNotes;
        Sequence = sequence;

        AddDomainEvent(new VersionDetailsUpdatedEvent(Id, Key, ProductId, Number, Name, Sequence, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves the target date.
    /// </summary>
    public Result MoveTargetDate(LocalDate? targetDate, string productName, EventActor actor, Instant timestamp)
    {
        if (StatusCategory is StatusCategory.Done or StatusCategory.Removed)
        {
            return Result.Failure("A released or withdrawn version cannot have its target date moved.");
        }

        if (targetDate == TargetDate)
        {
            return Result.Success();
        }

        var fromTargetDate = TargetDate;
        TargetDate = targetDate;

        AddDomainEvent(new VersionTargetDateMovedEvent(Id, Key, ProductId, productName, Number, fromTargetDate, targetDate, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Freezes scope and marks the version ready to ship.
    /// </summary>
    /// <param name="readyStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Ready"/>, resolved by the caller.
    /// </param>
    /// <remarks>
    /// Cutting is an artifact act and lives only here. A <see cref="Release"/> is announced, never cut —
    /// there is nothing to freeze scope on, because its scope is whichever versions it carries.
    /// </remarks>
    public Result Cut(LocalDate cutDate, StatusRef readyStatus, string productName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(readyStatus, nameof(readyStatus));

        if (CutDate is not null)
        {
            return Result.Failure("This version has already been cut.");
        }

        if (StatusCategory is StatusCategory.Done or StatusCategory.Removed)
        {
            return Result.Failure("A released or withdrawn version cannot be cut.");
        }

        CutDate = cutDate;
        ApplyStatus(readyStatus, actor, timestamp);

        AddDomainEvent(new VersionCutEvent(Id, Key, ProductId, productName, Number, cutDate, StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the version shipped.
    /// </summary>
    /// <param name="releasedStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Released"/>, resolved by the caller.
    /// </param>
    public Result MarkReleased(LocalDate releasedDate, StatusRef releasedStatus, string productName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(releasedStatus, nameof(releasedStatus));

        if (ReleasedDate is not null)
        {
            return Result.Failure("This version has already been released.");
        }

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("A withdrawn version cannot be released.");
        }

        if (CutDate is not null && releasedDate < CutDate)
        {
            return Result.Failure("The released date cannot be before the cut date.");
        }

        ReleasedDate = releasedDate;
        ApplyStatus(releasedStatus, actor, timestamp);

        AddDomainEvent(new VersionReleasedEvent(Id, Key, ProductId, productName, Number, releasedDate, StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that a version marked as shipped did not in fact ship, moving it back.
    /// </summary>
    /// <param name="toStatus">
    /// The status to return to: the one aliased <see cref="ProductStatusAlias.Ready"/> where the
    /// version was cut, otherwise its workflow's initial status. Resolved by the caller.
    /// </param>
    /// <remarks>
    /// This is not a withdrawal. Withdrawing says a real shipment was pulled; reverting says the
    /// shipment never happened and the record was wrong. Recording the first as the second leaves an
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
            return Result.Failure("This version has not been released, so there is nothing to revert.");
        }

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("A withdrawn version cannot be reverted. Its status is already terminal.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure("A reason is required to revert a version.");
        }

        var fromReleasedDate = ReleasedDate;
        ReleasedDate = null;
        ApplyStatus(toStatus, actor, timestamp, reason);

        AddDomainEvent(new VersionRevertedEvent(
            Id, Key, ProductId, productName, Number, fromReleasedDate.Value, reason.Trim(), StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Corrects the recorded target, cut and released dates without moving the version's status.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Cut"/> and <see cref="MarkReleased"/> assert that something happened, so each sets a
    /// date and applies a status together and refuses to run twice. Neither can fix a date entered
    /// wrongly. Without this, the only route to a corrected released date is to withdraw the version
    /// and release it again, which writes two status transitions that never happened into an
    /// append-only history.
    /// </para>
    /// <para>
    /// A correction says what was written down was wrong, not that the version moved, so status is
    /// left alone. Dates may be added as well as changed: a version can be marked released without
    /// ever being cut — historical import depends on it — so a cut date discovered later is a
    /// correction, not a lifecycle step.
    /// </para>
    /// <para>
    /// The target and cut dates may also be cleared, because each is only a record of something
    /// written down. The released date is the exception: emptying it on a released record would leave
    /// the status contradicting the dates. Recording that a version did not in fact ship is
    /// <see cref="RevertRelease"/>'s job, which moves the status to match.
    /// </para>
    /// <para>
    /// The one ordering rule that survives is real: a version cannot ship before it was cut.
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
            return Result.Failure("A withdrawn version cannot have its dates corrected.");
        }

        if (releasedDate is null && ReleasedDate is not null)
        {
            return Result.Failure(
                "A released version cannot have its released date removed. Revert the version instead.");
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

        AddDomainEvent(new VersionDatesCorrectedEvent(
            Id, Key, ProductId, productName, Number,
            fromTargetDate, targetDate, fromCutDate, cutDate, fromReleasedDate, releasedDate, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Pulls the version after it was cut. Phase one's failure proxy.
    /// </summary>
    /// <param name="withdrawnStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Withdrawn"/>, resolved by the caller.
    /// </param>
    public Result Withdraw(string? reason, StatusRef withdrawnStatus, string productName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(withdrawnStatus, nameof(withdrawnStatus));

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("This version has already been withdrawn.");
        }

        ApplyStatus(withdrawnStatus, actor, timestamp, reason);

        AddDomainEvent(new VersionWithdrawnEvent(Id, Key, ProductId, productName, Number, reason?.Trim(), StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Cuts a version against a product node.
    /// </summary>
    /// <param name="isProductReleasable">
    /// Whether the product's type permits versions. Supplied by the caller, which owns the type lookup.
    /// </param>
    public static Result<Version> Create(
        Guid productId,
        string number,
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
            return Result.Failure<Version>("Versions cannot be cut against this product's type.");
        }

        var version = new Version(productId, number, name, targetDate, sequence);
        version.ApplyStatus(initialStatus, actor, timestamp);

        // Deferred because Key is database-generated: an event raised here would carry Key 0.
        version.AddPostPersistenceAction(() => version.AddDomainEvent(new VersionPlannedEvent(
            version.Id,
            version.Key,
            version.ProductId,
            productName,
            version.Number,
            version.Name,
            version.TargetDate,
            version.StatusId,
            version.StatusCategory,
            actor,
            timestamp)));

        return Result.Success(version);
    }
}
