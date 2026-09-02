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
/// What was announced to customers — <c>Wayd 2026.07</c>. The product half of delivery: the shipment
/// as the market hears about it, gathering whatever versions and packages carried it.
/// </summary>
/// <remarks>
/// Distinct from <see cref="Version"/>, which is what was built. A release answers "what did we tell
/// customers?"; a version answers "what version of this one artifact?". <c>Release 2026.07 shipped
/// package WAYD-2026.07, containing Wayd API version 4.12.0.</c>
/// <para>
/// A release holds no cut date. Cutting freezes an artifact's scope and belongs to
/// <see cref="Version"/>; a release's scope is whichever versions and packages it carries, so there is
/// nothing to freeze independently of them.
/// </para>
/// <para>
/// Contents may arrive either way. Most run through a <see cref="ReleasePackage"/>, which is the
/// deployment unit; a single-artifact announcement carries its <see cref="Version"/> directly rather
/// than inventing a package of one. Both routes are set together, because what a release must never do
/// is count the same version twice — see <see cref="SetContents"/>.
/// </para>
/// </remarks>
public sealed class Release : StatusTrackedEntity, IHasIdAndKey
{
    private readonly List<ReleaseVersion> _versions = [];
    private readonly List<ReleasePackageInclusion> _packages = [];

    private Release() { }

    private Release(Guid? productId, string version, string? name, LocalDate? targetDate, long? sequence)
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
    /// The product node this release is announced under, where the organization scopes it to one.
    /// </summary>
    /// <remarks>
    /// Optional, and typically a product line rather than a leaf: <c>Wayd 2026.07</c> announces work
    /// across the API, the client and the MCP server, so requiring a single owner would force a
    /// misleading choice between them. A release spanning product lines leaves this null.
    /// <para>
    /// Deliberately <em>not</em> gated on <c>ProductType.IsReleasable</c>. That gate asks whether an
    /// artifact can be cut against a node, which is <see cref="Version"/>'s question. A product line is
    /// usually not releasable and is exactly what an announcement sits under.
    /// </para>
    /// </remarks>
    public Guid? ProductId { get; private set; }

    /// <summary>
    /// The product this release is announced under, when one is loaded.
    /// </summary>
    /// <remarks>
    /// For the read side only. Domain methods take the product name they need as an argument, so no
    /// invariant depends on this being loaded.
    /// </remarks>
    public Product? Product { get; private init; }

    /// <summary>
    /// The release as the organization announces it — <c>2026.07</c>, <c>Spring Release</c>, <c>R4</c>.
    /// </summary>
    /// <remarks>
    /// <strong>Free text, never parsed.</strong> Nothing may compare, sort, or extract meaning from this
    /// string; ordering comes from <see cref="ReleasedDate"/>, then <see cref="Sequence"/>.
    /// <para>
    /// Distinct from the version strings of the artifacts it carries: <c>2026.07</c> is the
    /// announcement's own label, and <c>4.12.0</c> belongs to <see cref="Version.Number"/>.
    /// </para>
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
    /// Normally null; releases order by date. User-supplied, unlike <c>ProjectScore.Sequence</c>.
    /// </remarks>
    public long? Sequence { get; private set; }

    /// <summary>
    /// When the release is expected to be announced.
    /// </summary>
    public LocalDate? TargetDate { get; private set; }

    /// <summary>
    /// When it was actually announced. Set by <see cref="MarkReleased"/>.
    /// </summary>
    public LocalDate? ReleasedDate { get; private set; }

    /// <summary>
    /// Product notes for this release — <c>Scoring now supports weighted criteria</c>.
    /// </summary>
    /// <remarks>
    /// Written for customers. Distinct from <see cref="Version.Notes"/>, which records what changed in
    /// the artifact for an engineering reader.
    /// </remarks>
    public string? Notes
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The versions this release announces directly, outside any package.
    /// </summary>
    public IReadOnlyCollection<ReleaseVersion> Versions => _versions.AsReadOnly();

    /// <summary>
    /// The packages this release shipped.
    /// </summary>
    public IReadOnlyCollection<ReleasePackageInclusion> Packages => _packages.AsReadOnly();

    /// <summary>
    /// Whether this release announces anything at all.
    /// </summary>
    /// <remarks>
    /// An empty release is legitimate and is not a draft: a repackaging or a pricing change is
    /// announced with nothing deployed. It is only <see cref="MarkReleased"/> that cares, and only to
    /// the extent of refusing to ship an announcement whose contents have not.
    /// </remarks>
    public bool IsEmpty => _versions.Count == 0 && _packages.Count == 0;

    /// <summary>
    /// Replaces everything this release announces — the packages it shipped and the versions it
    /// carries directly — as one set.
    /// </summary>
    /// <param name="versionIds">The versions to carry directly. Empty removes them all.</param>
    /// <param name="packageIds">The packages to ship. Empty removes them all.</param>
    /// <param name="versionIdsInPackages">
    /// Every version reachable through <paramref name="packageIds"/>, resolved by the caller — the
    /// aggregate cannot load a package's manifest. A version in this set may not also be carried
    /// directly.
    /// </param>
    /// <remarks>
    /// Whole-set replacement rather than incremental, matching the manifest: a release's contents are a
    /// set, and a partially-applied change would claim a combination that was never announced. Both
    /// empty clears the release, which is a legitimate state — see <see cref="IsEmpty"/>.
    /// <para>
    /// Both routes move together because the invariant spans them. A version shipped inside a package
    /// and also listed directly would be announced twice by one release, which makes "what did 2026.07
    /// contain" answerable two ways. Splitting this across two calls would split that invariant across
    /// two transactions: each would have to judge the double-count against a different baseline, and
    /// swapping a directly-carried version for the package that contains it — one valid change of mind
    /// — would be reachable only by performing the two halves in one particular order.
    /// </para>
    /// <para>
    /// The double-count is therefore resolved against what the release will contain <em>afterwards</em>,
    /// which is the only baseline that was ever correct. A version arriving by both routes at once is
    /// the caller's error to fix, so it is refused rather than silently deduplicated: the package is the
    /// deployment unit and would win, but guessing which route the caller meant would announce a
    /// shipment they did not ask for.
    /// </para>
    /// </remarks>
    public Result SetContents(
        IReadOnlyCollection<Guid> versionIds,
        IReadOnlyCollection<Guid> packageIds,
        IReadOnlyCollection<Guid> versionIdsInPackages,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(versionIds, nameof(versionIds));
        Guard.Against.Null(packageIds, nameof(packageIds));
        Guard.Against.Null(versionIdsInPackages, nameof(versionIdsInPackages));

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("A withdrawn release's contents cannot be amended.");
        }

        // Once announced, the contents are the record of what shipped rather than a plan.
        if (ReleasedDate is not null)
        {
            return Result.Failure("A released release's contents cannot be amended.");
        }

        var distinctVersions = versionIds.Distinct().ToList();
        if (distinctVersions.Count != versionIds.Count)
        {
            return Result.Failure("A version can appear only once in a release.");
        }

        var distinctPackages = packageIds.Distinct().ToList();
        if (distinctPackages.Count != packageIds.Count)
        {
            return Result.Failure("A package can appear only once in a release.");
        }

        var doubleCounted = distinctVersions.Intersect(versionIdsInPackages).Any();
        if (doubleCounted)
        {
            return Result.Failure(
                "A version shipping inside one of this release's packages cannot also be carried directly. Where a package exists it is the unit, so that one shipment is announced once.");
        }

        // Order is not part of the comparison: the contents are a set.
        var versionsUnchanged = distinctVersions.Count == _versions.Count
            && distinctVersions.All(id => _versions.Any(v => v.VersionId == id));
        var packagesUnchanged = distinctPackages.Count == _packages.Count
            && distinctPackages.All(id => _packages.Any(p => p.PackageId == id));

        if (versionsUnchanged && packagesUnchanged)
        {
            return Result.Success();
        }

        _versions.Clear();
        foreach (var versionId in distinctVersions)
        {
            _versions.Add(new ReleaseVersion(Id, versionId));
        }

        _packages.Clear();
        foreach (var packageId in distinctPackages)
        {
            _packages.Add(new ReleasePackageInclusion(Id, packageId));
        }

        AddDomainEvent(new ReleaseContentsChangedEvent(
            Id, Key, Version, _versions.Count, _packages.Count, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Updates the release's own version label, name, notes or ordering sequence.
    /// </summary>
    /// <remarks>
    /// Raises nothing when every value already matches. Compares trimmed input because the setters trim.
    /// </remarks>
    public Result UpdateDetails(
        string version, string? name, string? notes, Guid? productId, long? sequence, EventActor actor, Instant timestamp)
    {
        var newVersion = Guard.Against.NullOrWhiteSpace(version, nameof(version)).Trim();
        var newName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var newNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (string.Equals(Version, newVersion, StringComparison.Ordinal)
            && string.Equals(Name, newName, StringComparison.Ordinal)
            && string.Equals(Notes, newNotes, StringComparison.Ordinal)
            && ProductId == productId
            && Sequence == sequence)
        {
            return Result.Success();
        }

        Version = newVersion;
        Name = newName;
        Notes = newNotes;
        ProductId = productId;
        Sequence = sequence;

        AddDomainEvent(new ReleaseDetailsUpdatedEvent(Id, Key, ProductId, Version, Name, Sequence, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves the target date.
    /// </summary>
    public Result MoveTargetDate(LocalDate? targetDate, EventActor actor, Instant timestamp)
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

        AddDomainEvent(new ReleaseTargetDateMovedEvent(Id, Key, ProductId, Version, fromTargetDate, targetDate, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the release was announced.
    /// </summary>
    /// <param name="releasedStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Released"/>, resolved by the caller.
    /// </param>
    /// <param name="hasUnreleasedContents">
    /// Whether any version or package this release carries has yet to ship, resolved by the caller —
    /// the aggregate holds ids, not the records themselves.
    /// </param>
    /// <remarks>
    /// Named <c>MarkReleased</c> because C# forbids a member matching its type's name.
    /// <para>
    /// The unreleased-contents rule is what makes an announcement mean something: telling customers
    /// that <c>2026.07</c> shipped while a version inside it has not is the one claim a release can
    /// make that its own contents contradict. Everything else about a release is a matter of record;
    /// this is the only real invariant it has.
    /// </para>
    /// </remarks>
    public Result MarkReleased(
        LocalDate releasedDate, bool hasUnreleasedContents, StatusRef releasedStatus, EventActor actor, Instant timestamp)
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

        if (hasUnreleasedContents)
        {
            return Result.Failure(
                "This release carries a version or package that has not shipped. Release those first, or remove them from this release.");
        }

        ReleasedDate = releasedDate;
        ApplyStatus(releasedStatus, actor, timestamp);

        AddDomainEvent(new ReleaseReleasedEvent(
            Id, Key, ProductId, Version, releasedDate, _versions.Count, _packages.Count, StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that a release marked as announced was not in fact announced, moving it back.
    /// </summary>
    /// <param name="toStatus">The status to return to, resolved by the caller.</param>
    /// <remarks>
    /// This is not a withdrawal. Withdrawing says a real announcement was retracted; reverting says the
    /// announcement never happened and the record was wrong. Recording the first as the second leaves
    /// an append-only history asserting a retraction nobody performed.
    /// </remarks>
    public Result RevertRelease(StatusRef toStatus, string reason, EventActor actor, Instant timestamp)
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
            Id, Key, ProductId, Version, fromReleasedDate.Value, reason.Trim(), StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Corrects the recorded target and released dates without moving the release's status.
    /// </summary>
    /// <remarks>
    /// No cut date here, unlike <see cref="Version.CorrectDates"/> — a release is never cut. The
    /// released date may be added or changed but not cleared, because emptying it on an announced
    /// release would leave the status contradicting the dates; <see cref="RevertRelease"/> is the
    /// action for that.
    /// </remarks>
    public Result CorrectDates(
        LocalDate? targetDate,
        LocalDate? releasedDate,
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

        if (targetDate == TargetDate && releasedDate == ReleasedDate)
        {
            return Result.Success();
        }

        var fromTargetDate = TargetDate;
        var fromReleasedDate = ReleasedDate;
        TargetDate = targetDate;
        ReleasedDate = releasedDate;

        AddDomainEvent(new ReleaseDatesCorrectedEvent(
            Id, Key, ProductId, Version,
            fromTargetDate, targetDate, fromReleasedDate, releasedDate, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Retracts the release after it was announced.
    /// </summary>
    /// <param name="withdrawnStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Withdrawn"/>, resolved by the caller.
    /// </param>
    /// <remarks>
    /// Withdrawing an announcement says nothing about the versions it carried. An artifact that shipped
    /// has shipped whatever the market was later told, so each version keeps its own status and is
    /// withdrawn separately where it too was pulled.
    /// </remarks>
    public Result Withdraw(string? reason, StatusRef withdrawnStatus, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(withdrawnStatus, nameof(withdrawnStatus));

        if (StatusCategory == StatusCategory.Removed)
        {
            return Result.Failure("This release has already been withdrawn.");
        }

        ApplyStatus(withdrawnStatus, actor, timestamp, reason);

        AddDomainEvent(new ReleaseWithdrawnEvent(Id, Key, ProductId, Version, reason?.Trim(), StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Plans a release, optionally under a product node.
    /// </summary>
    /// <remarks>
    /// Contents are attached afterwards rather than here. Unlike a package — which must be assembled
    /// from at least one component, because an empty manifest says nothing about what shipped — an
    /// empty release is a real state: the announcement is drafted before anyone knows which versions
    /// will make it.
    /// </remarks>
    public static Result<Release> Create(
        Guid? productId,
        string version,
        string? name,
        LocalDate? targetDate,
        long? sequence,
        StatusRef initialStatus,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(initialStatus, nameof(initialStatus));

        var release = new Release(productId, version, name, targetDate, sequence);
        release.ApplyStatus(initialStatus, actor, timestamp);

        // Deferred because Key is database-generated: an event raised here would carry Key 0.
        release.AddPostPersistenceAction(() => release.AddDomainEvent(new ReleasePlannedEvent(
            release.Id,
            release.Key,
            release.ProductId,
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
