using System.Globalization;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Imports;

/// <summary>
/// Imports intake requests onto their desks, each submitted when the row says and then moved to the status
/// its review has reached.
/// </summary>
/// <remarks>
/// A request is created the way an upstream system creates one (<see cref="IntakeRequest.Create"/>): any
/// open desk, any active channel, and the origin it came from. It then walks the steps an agent would take
/// — assignee, tags, status — so its history reads as the moves the row describes rather than a record
/// that appeared fully formed.
/// <para>
/// Per row: a request depends on nothing else in the file. A duplicate names a request already in Wayd,
/// never another row, so no row waits on another.
/// </para>
/// <para>
/// Gated on administering Demand Management rather than an import permission: who may act on a request is
/// decided by desk role, and the only role spanning every desk is the Demand Management Admin.
/// </para>
/// </remarks>
public sealed class IntakeRequestImportDefinition(
    IDemandManagementDbContext dbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IFeatureFlags featureFlags,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportIntakeRequestDto>(serializer)
{
    public const string ImportKey = "demand-management.intake-requests";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IFeatureFlags _featureFlags = featureFlags;

    public override string Key => ImportKey;
    public override string DisplayName => "Intake Requests";
    public override string PermissionAction => ApplicationAction.Administer;
    public override string PermissionResource => ApplicationResource.IntakeRequests;

    public override int MaxRows => 10_000;

    /// <inheritdoc />
    public override Task<bool> IsEnabled(CancellationToken cancellationToken) =>
        _featureFlags.IsEnabled(FeatureFlags.Names.DemandManagement, cancellationToken);

    protected override IReadOnlyList<ImportPass<ImportIntakeRequestDto>> Steps =>
    [
        new("CreateIntakeRequests", ImportPassScope.Chunked, CreateIntakeRequests),
    ];

    /// <inheritdoc />
    public override async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> DescribeCreatedRecords(
        IReadOnlyCollection<Guid> createdEntityIds,
        CancellationToken cancellationToken) =>
        await _dbContext.IntakeRequests
            .Where(x => createdEntityIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Key, x.Title })
            .ToDictionaryAsync(
                x => x.Id,
                x => new ImportRecordLink(ImportRecordType.IntakeRequest, x.Key.ToString(CultureInfo.InvariantCulture), x.Title),
                cancellationToken);

    private async Task<Result> CreateIntakeRequests(ImportPassContext<ImportIntakeRequestDto> context, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var userId = _currentUser.GetUserId();

        var references = await Resolve(context, cancellationToken);
        Dictionary<Guid, Result<StatusWorkflow>> workflowsByDeskId = [];
        HashSet<(string, string)> takenOrigins = [.. references.TakenOrigins];

        foreach (var row in context.Accepted)
        {
            var data = row.Data;

            if (!references.DesksById.TryGetValue(data.DeskId, out var desk))
            {
                row.Failed($"No desk was found with id '{data.DeskId}'.");
                continue;
            }

            if (MissingEmployee(data, references.EmployeeIds) is { } missing)
            {
                row.Failed(missing);
                continue;
            }

            IntakeChannel? channel = null;
            if (data.ChannelId is { } channelId && !references.ChannelsById.TryGetValue(channelId, out channel))
            {
                row.Failed($"No channel was found with id '{channelId}'.");
                continue;
            }

            if (data.AssigneeId is { } assigneeId && !IsActiveMember(desk, assigneeId, references.ActiveEmployeeIds))
            {
                row.Failed($"The assignee '{assigneeId}' must be an active member of the request's desk.");
                continue;
            }

            var needBy = NeedBy(data.NeedBy);
            if (needBy.IsFailure)
            {
                row.Failed(needBy.Error);
                continue;
            }

            var origin = Origin(data.Origin, takenOrigins);
            if (origin.IsFailure)
            {
                row.Failed(origin.Error);
                continue;
            }

            if (!workflowsByDeskId.TryGetValue(desk.Id, out var workflow))
            {
                workflow = await _statusResolver.ForDesk(desk.Id, cancellationToken);
                workflowsByDeskId[desk.Id] = workflow;
            }

            if (workflow.IsFailure)
            {
                row.Failed($"The desk's workflow could not be read: {workflow.Error}");
                continue;
            }

            var statuses = Statuses(workflow.Value, data.Status);
            if (statuses.IsFailure)
            {
                row.Failed(statuses.Error);
                continue;
            }

            var submittedById = data.SubmittedById ?? data.RequesterId;
            var submittedAt = data.SubmittedAt ?? now;

            var created = IntakeRequest.Create(
                data.Title,
                data.Description,
                data.ExpectedBenefits,
                data.RequesterId,
                submittedById,
                desk,
                channel,
                needBy.Value,
                origin.Value,
                statuses.Value.Submitted,
                EventActor.Import(userId, submittedById),
                submittedAt);
            if (created.IsFailure)
            {
                row.Failed(created.Error);
                continue;
            }

            var request = created.Value;

            // The review steps are an agent's, recorded against whoever owns the request when the row names one.
            var reviewer = EventActor.Import(userId, data.AssigneeId);

            if (data.AssigneeId is not null)
                request.ChangeAssignee(data.AssigneeId, reviewer, submittedAt);

            var tagged = ApplyTags(request, desk, data.TagIds, references, reviewer, submittedAt);
            if (tagged.IsFailure)
            {
                row.Failed(tagged.Error);
                continue;
            }

            var moved = Move(request, data, statuses.Value.Target, references.SurvivorsById, userId,
                reviewer, data.StatusChangedAt ?? submittedAt);
            if (moved.IsFailure)
            {
                row.Failed(moved.Error);
                continue;
            }

            if (request.Origin is { } added)
                takenOrigins.Add(OriginKey(added.SourceSystem, added.ExternalId));

            await _dbContext.IntakeRequests.AddAsync(request, cancellationToken);
            row.Created(request.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Moves a new request on to the status its row names. Withdrawn is the requester's own move; every other
    /// status is an agent's.
    /// </summary>
    private static Result Move(
        IntakeRequest request,
        ImportIntakeRequestDto data,
        StatusRef? target,
        IReadOnlyDictionary<Guid, IntakeRequest> survivorsById,
        string? userId,
        EventActor reviewer,
        Instant timestamp)
    {
        // A request left where every request starts has no move to give a reason or a time to, so either one
        // would be dropped without a word.
        if (target is null)
        {
            if (data.DuplicateOfId is not null)
                return Result.Failure("Only a request imported as a duplicate names DuplicateOfId.");
            if (data.StatusReason is not null)
                return Result.Failure("StatusReason needs a Status the request moves to; it stays submitted.");
            if (data.StatusChangedAt is not null)
                return Result.Failure("StatusChangedAt needs a Status the request moves to; it stays submitted.");

            return Result.Success();
        }

        var alias = (IntakeRequestStatusAlias)target.Alias;

        if (alias == IntakeRequestStatusAlias.Withdrawn)
        {
            if (data.DuplicateOfId is not null)
                return Result.Failure("Only a request imported as a duplicate names DuplicateOfId.");

            return request.Withdraw(request.RequesterId, target, data.StatusReason,
                EventActor.Import(userId, request.RequesterId), timestamp);
        }

        IntakeRequest? survivor = null;
        if (data.DuplicateOfId is { } survivorId && !survivorsById.TryGetValue(survivorId, out survivor))
            return Result.Failure($"No intake request was found with id '{survivorId}' for this one to duplicate.");

        return request.ChangeStatus(target, data.StatusReason, survivor, reviewer, timestamp);
    }

    private static Result ApplyTags(
        IntakeRequest request,
        IntakeDesk desk,
        IReadOnlyList<Guid> tagIds,
        ResolvedReferences references,
        EventActor actor,
        Instant timestamp)
    {
        HashSet<Guid> singleChoiceAxesUsed = [];

        foreach (var tagId in tagIds.Distinct())
        {
            if (!references.TagsById.TryGetValue(tagId, out var tag))
                return Result.Failure($"No intake request tag was found with id '{tagId}'.");

            IntakeRequestTagCategory? category = null;
            if (tag.CategoryId is { } categoryId && !references.CategoriesById.TryGetValue(categoryId, out category))
                return Result.Failure($"No intake request tag axis was found for tag '{tag.Name}'.");

            // Tagging replaces the tag already on a single-choice axis, which would keep only the last one.
            if (category is { AllowsMany: false } && !singleChoiceAxesUsed.Add(category.Id))
                return Result.Failure($"'{category.Name}' takes one tag, and the row names more than one.");

            var tagged = request.Tag(tag, category, desk, actor, timestamp);
            if (tagged.IsFailure)
                return Result.Failure($"Could not apply tag '{tag.Name}': {tagged.Error}");
        }

        return Result.Success();
    }

    /// <summary>
    /// The status a request starts in on the desk's workflow, and the one its row names, or null where the row
    /// leaves it there.
    /// </summary>
    /// <remarks>
    /// A row names a status by its name first, since that is what the person exporting a backlog sees, and by
    /// its meaning second, so a file written for the default workflow still lands on a desk that renamed its
    /// stages.
    /// </remarks>
    private static Result<(StatusRef Submitted, StatusRef? Target)> Statuses(StatusWorkflow workflow, string? status)
    {
        var submitted = workflow.StatusFor((int)IntakeRequestStatusAlias.Submitted) is { } initial
            ? workflow.TargetRef(initial.Id)
            : null;
        if (submitted is null)
            return Result.Failure<(StatusRef, StatusRef?)>($"'{workflow.Name}' has no status meaning Submitted.");

        if (string.IsNullOrWhiteSpace(status))
            return (submitted, null);

        var name = status.Trim();
        var match = workflow.Statuses.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? (Meaning(name) is { } alias ? workflow.StatusFor((int)alias) : null);

        if (match is null)
            return Result.Failure<(StatusRef, StatusRef?)>($"'{workflow.Name}' has no status named or meaning '{name}'.");

        return (submitted, match.Id == submitted.StatusId ? null : workflow.TargetRef(match.Id));
    }

    /// <summary>
    /// The status meaning a cell names, spaced or not (<c>Waiting on Requester</c> or <c>WaitingOnRequester</c>).
    /// </summary>
    /// <remarks>
    /// Matched against the names rather than through <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>,
    /// which also reads numbers and combines comma-separated names into an unrelated value.
    /// </remarks>
    private static IntakeRequestStatusAlias? Meaning(string name)
    {
        var compact = name.Replace(" ", "", StringComparison.Ordinal);
        foreach (var alias in Enum.GetValues<IntakeRequestStatusAlias>())
        {
            if (alias != IntakeRequestStatusAlias.None && string.Equals(alias.ToString(), compact, StringComparison.OrdinalIgnoreCase))
                return alias;
        }

        return null;
    }

    private static Result<IntakeRequestNeedBy?> NeedBy(ImportIntakeRequestNeedByDto? needBy)
    {
        if (needBy is null)
            return Result.Success<IntakeRequestNeedBy?>(null);

        var created = IntakeRequestNeedBy.Create(needBy.Date, needBy.Basis, needBy.Reference, needBy.CommittedById, needBy.CommittedOn);
        return created.IsFailure
            ? Result.Failure<IntakeRequestNeedBy?>(created.Error)
            : created.Value;
    }

    private static Result<IntakeRequestOrigin?> Origin(ImportIntakeRequestOriginDto? origin, HashSet<(string, string)> taken)
    {
        if (origin is null)
            return Result.Success<IntakeRequestOrigin?>(null);

        var created = IntakeRequestOrigin.Create(origin.SourceSystem, origin.ExternalId, origin.Url);
        if (created.IsFailure)
            return Result.Failure<IntakeRequestOrigin?>(created.Error);

        // Checked within the file as well as against the database: rows are applied before anything is saved.
        if (taken.Contains(OriginKey(created.Value.SourceSystem, created.Value.ExternalId)))
        {
            return Result.Failure<IntakeRequestOrigin?>(
                $"A request from '{created.Value.SourceSystem}' with external id '{created.Value.ExternalId}' already exists.");
        }

        return created.Value;
    }

    private static string? MissingEmployee(ImportIntakeRequestDto data, HashSet<Guid> employeeIds)
    {
        if (!employeeIds.Contains(data.RequesterId))
            return $"No employee was found with id '{data.RequesterId}' to be the requester.";

        if (data.SubmittedById is { } submittedById && !employeeIds.Contains(submittedById))
            return $"No employee was found with id '{submittedById}' to have submitted this request.";

        if (data.NeedBy?.CommittedById is { } committedById && !employeeIds.Contains(committedById))
            return $"No employee was found with id '{committedById}' to have made the commitment.";

        return null;
    }

    /// <summary>Whether an employee can own a request on the desk: a member of it, and still with the company.</summary>
    private static bool IsActiveMember(IntakeDesk desk, Guid employeeId, HashSet<Guid> activeEmployeeIds) =>
        activeEmployeeIds.Contains(employeeId) && desk.Members.Any(m => m.EmployeeId == employeeId);

    /// <summary>The comparison the origin's unique index makes, which is case-insensitive under the database's collation.</summary>
    private static (string, string) OriginKey(string sourceSystem, string externalId) =>
        (sourceSystem.ToUpperInvariant(), externalId.ToUpperInvariant());

    /// <summary>Loads every record the chunk's rows point at, once.</summary>
    private async Task<ResolvedReferences> Resolve(ImportPassContext<ImportIntakeRequestDto> context, CancellationToken cancellationToken)
    {
        var rows = context.Rows.Select(r => r.Data).ToList();

        var deskIds = rows.Select(r => r.DeskId).Distinct().ToList();
        var desks = await _dbContext.IntakeDesks
            .Include(d => d.Members)
            .Include(d => d.Channels)
            .Include(d => d.TagCategories)
            .Where(d => deskIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var channelIds = rows.Where(r => r.ChannelId is not null).Select(r => r.ChannelId!.Value).Distinct().ToList();
        var channels = await _dbContext.IntakeChannels
            .Where(c => channelIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var employeeIds = rows
            .SelectMany(r => new[] { (Guid?)r.RequesterId, r.SubmittedById, r.AssigneeId, r.NeedBy?.CommittedById })
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var employees = await _dbContext.Employees
            .Where(e => employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.IsActive })
            .ToListAsync(cancellationToken);

        var tagIds = rows.SelectMany(r => r.TagIds).Distinct().ToList();
        var tags = await _dbContext.IntakeRequestTags
            .Where(t => tagIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var categoryIds = tags.Values.Where(t => t.CategoryId is not null).Select(t => t.CategoryId!.Value).Distinct().ToList();
        var categories = await _dbContext.IntakeRequestTagCategories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var survivorIds = rows.Where(r => r.DuplicateOfId is not null).Select(r => r.DuplicateOfId!.Value).Distinct().ToList();
        var survivors = await _dbContext.IntakeRequests
            .Where(r => survivorIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var externalIds = rows.Where(r => r.Origin is not null).Select(r => r.Origin!.ExternalId.Trim()).Distinct().ToList();
        var origins = externalIds.Count == 0
            ? []
            : await _dbContext.IntakeRequests
                .Where(r => r.Origin != null && externalIds.Contains(r.Origin.ExternalId))
                .Select(r => new { r.Origin!.SourceSystem, r.Origin.ExternalId })
                .ToListAsync(cancellationToken);

        return new ResolvedReferences(
            desks,
            channels,
            [.. employees.Select(e => e.Id)],
            [.. employees.Where(e => e.IsActive).Select(e => e.Id)],
            tags,
            categories,
            survivors,
            [.. origins.Select(o => OriginKey(o.SourceSystem, o.ExternalId))]);
    }

    /// <summary>The records a chunk's rows point at, by id.</summary>
    private sealed record ResolvedReferences(
        Dictionary<Guid, IntakeDesk> DesksById,
        Dictionary<Guid, IntakeChannel> ChannelsById,
        HashSet<Guid> EmployeeIds,
        HashSet<Guid> ActiveEmployeeIds,
        Dictionary<Guid, IntakeRequestTag> TagsById,
        Dictionary<Guid, IntakeRequestTagCategory> CategoriesById,
        Dictionary<Guid, IntakeRequest> SurvivorsById,
        HashSet<(string, string)> TakenOrigins);
}
