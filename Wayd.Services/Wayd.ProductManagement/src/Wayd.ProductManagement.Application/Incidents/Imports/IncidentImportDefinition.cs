using System.Globalization;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Incidents.Imports;

/// <summary>
/// Imports incidents, each created open and then walked to the status its row names.
/// </summary>
/// <remarks>
/// <para>
/// Per row. An incident depends on nothing else in the file, and it has no natural key: submitting the
/// same file again creates another incident for every row. A rejected row keeps out only itself.
/// </para>
/// <para>
/// The walk is the aggregate's own moves, on the instance just created, and the row is added only after
/// the last move succeeds. Status fields are never written directly. A blank <c>ImportId</c> fails the
/// row here, because the runner would otherwise number it by position.
/// </para>
/// </remarks>
public sealed class IncidentImportDefinition(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    IFeatureFlags featureFlags,
    ILogger<IncidentImportDefinition> logger,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportIncidentDto>(serializer)
{
    public const string ImportKey = "product-management.incidents";

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IFeatureFlags _featureFlags = featureFlags;
    private readonly ILogger<IncidentImportDefinition> _logger = logger;

    public override string Key => ImportKey;

    public override string DisplayName => "Incidents";

    public override string PermissionAction => ApplicationAction.Import;

    public override string PermissionResource => ApplicationResource.Incidents;

    public override ImportAtomicity Atomicity => ImportAtomicity.PerRow;

    /// <summary>
    /// The same cap a preflight can hold. The pass saves chunk by chunk, and a preflight cannot release
    /// those locks per chunk, so the check and the run share the deployments ceiling.
    /// </summary>
    public override int MaxRows => 10_000;

    /// <inheritdoc />
    public override async Task<bool> IsEnabled(CancellationToken cancellationToken) =>
        await _featureFlags.IsEnabled(FeatureFlags.Names.ProductManagement, cancellationToken)
        && await _featureFlags.IsEnabled(FeatureFlags.Names.IncidentManagement, cancellationToken);

    protected override IReadOnlyList<ImportPass<ImportIncidentDto>> Steps =>
    [
        new("CreateIncidents", ImportPassScope.Chunked, CreateIncidents),
    ];

    /// <inheritdoc />
    public override async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> DescribeCreatedRecords(
        IReadOnlyCollection<Guid> createdEntityIds,
        CancellationToken cancellationToken) =>
        await _productManagementDbContext.Incidents
            .AsNoTracking()
            .Where(x => createdEntityIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Key, x.Title })
            .ToDictionaryAsync(
                x => x.Id,
                x => new ImportRecordLink(ImportRecordType.Incident, x.Key.ToString(CultureInfo.InvariantCulture), x.Title),
                cancellationToken);

    private async Task<Result> CreateIncidents(ImportPassContext<ImportIncidentDto> context, CancellationToken cancellationToken)
    {
        var submittedBy = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (submittedBy is null || submittedBy == Guid.Empty)
        {
            return Result.Failure("A linked employee is required to import incidents.");
        }

        var now = _dateTimeProvider.Now;
        var actor = EventActor.Import(_currentUser.GetUserId());

        var statuses = await ResolveStatuses(cancellationToken);
        if (statuses.IsFailure)
        {
            return Result.Failure(statuses.Error);
        }

        var levelsByName = await ResolveLevels(cancellationToken);
        if (levelsByName.IsFailure)
        {
            return Result.Failure(levelsByName.Error);
        }

        var employeeIds = await ResolveEmployeeIds(context, cancellationToken);
        var productIds = await ResolveProductIds(context, cancellationToken);
        var releaseIds = await ResolveReleaseIds(context, cancellationToken);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;

            if (string.IsNullOrWhiteSpace(data.ImportId))
            {
                row.Failed("ImportId is required.");
                continue;
            }

            if (!levelsByName.Value.TryGetValue(data.Severity.Trim(), out var severityLevelId))
            {
                row.Failed($"No severity level named '{data.Severity.Trim()}' is in the selected set.");
                continue;
            }

            if (data.CommanderEmployeeId is Guid commanderId
                && commanderId != Guid.Empty
                && !employeeIds.Contains(commanderId))
            {
                row.Failed($"No employee was found with id '{commanderId}'.");
                continue;
            }

            if (data.ReportedByEmployeeId is Guid reportedById
                && reportedById != Guid.Empty
                && !employeeIds.Contains(reportedById))
            {
                row.Failed($"No employee was found with id '{reportedById}'.");
                continue;
            }

            var missingProduct = Missing(data.ProductIds, productIds);
            if (missingProduct is not null)
            {
                row.Failed($"No product was found with id '{missingProduct}'.");
                continue;
            }

            var missingRelease = Missing(data.ReleaseIds, releaseIds);
            if (missingRelease is not null)
            {
                row.Failed($"No release was found with id '{missingRelease}'.");
                continue;
            }

            var commander = data.CommanderEmployeeId is null || data.CommanderEmployeeId == Guid.Empty
                ? null
                : data.CommanderEmployeeId;
            var reportedBy = data.ReportedByEmployeeId is null || data.ReportedByEmployeeId == Guid.Empty
                ? submittedBy.Value
                : data.ReportedByEmployeeId.Value;

            var created = Incident.Create(
                data.Title,
                data.Symptoms,
                data.RootCause,
                commander,
                severityLevelId,
                data.NoticedAt ?? now,
                data.StartedAt,
                data.RecoveredAt,
                data.ProductIds,
                statuses.Value.Open,
                actor,
                now,
                submittedBy.Value,
                reportedBy,
                data.ReleaseIds,
                data.Remediation);

            if (created.IsFailure)
            {
                row.Failed(created.Error);
                continue;
            }

            var walked = Walk(created.Value, data, statuses.Value, actor, now);
            if (walked.IsFailure)
            {
                row.Failed(walked.Error);
                continue;
            }

            await _productManagementDbContext.Incidents.AddAsync(created.Value, cancellationToken);
            row.Created(created.Value.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Moves the incident through the aliases between Open and the row's status. Cancel stays a cancel
    /// from Open: the workflow allows it, and a response start on the row is already on the record from
    /// create. A start is not filled by canceling.
    /// </summary>
    private static Result Walk(
        Incident incident,
        ImportIncidentDto data,
        IncidentStatuses statuses,
        EventActor actor,
        Instant timestamp)
    {
        switch (data.Status)
        {
            case ProductStatusAlias.Open:
                return Result.Success();
            case ProductStatusAlias.Responding:
                return incident.BeginResponding(statuses.Responding, actor, timestamp, data.StartedAt);
            case ProductStatusAlias.Resolved:
                return Recover(incident, data, statuses, actor, timestamp);
            case ProductStatusAlias.Closed:
                var recovered = Recover(incident, data, statuses, actor, timestamp);
                return recovered.IsFailure
                    ? recovered
                    : incident.Close(statuses.Closed, actor, timestamp);
            case ProductStatusAlias.Canceled:
                return incident.Cancel(statuses.Canceled, actor, timestamp);
            default:
                return Result.Failure($"'{data.Status}' is not an incident status.");
        }
    }

    /// <summary>
    /// Begins response, then marks recovered. When the file has a recovery and no response start, the
    /// recovery instant is the start as well. Beginning response at import time would put the start
    /// after a historical recovery, which the aggregate refuses.
    /// </summary>
    private static Result Recover(
        Incident incident,
        ImportIncidentDto data,
        IncidentStatuses statuses,
        EventActor actor,
        Instant timestamp)
    {
        var start = data.StartedAt ?? data.RecoveredAt;
        var responding = incident.BeginResponding(statuses.Responding, actor, timestamp, start);
        if (responding.IsFailure)
        {
            return responding;
        }

        return incident.MarkRecovered(statuses.Resolved, actor, timestamp, data.RecoveredAt);
    }

    private async Task<Result<IncidentStatuses>> ResolveStatuses(CancellationToken cancellationToken)
    {
        var open = await Resolve(ProductStatusAlias.Open, cancellationToken);
        if (open.IsFailure)
        {
            return Result.Failure<IncidentStatuses>(open.Error);
        }

        var responding = await Resolve(ProductStatusAlias.Responding, cancellationToken);
        if (responding.IsFailure)
        {
            return Result.Failure<IncidentStatuses>(responding.Error);
        }

        var resolved = await Resolve(ProductStatusAlias.Resolved, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<IncidentStatuses>(resolved.Error);
        }

        var closed = await Resolve(ProductStatusAlias.Closed, cancellationToken);
        if (closed.IsFailure)
        {
            return Result.Failure<IncidentStatuses>(closed.Error);
        }

        var canceled = await Resolve(ProductStatusAlias.Canceled, cancellationToken);
        if (canceled.IsFailure)
        {
            return Result.Failure<IncidentStatuses>(canceled.Error);
        }

        return Result.Success(new IncidentStatuses(open.Value, responding.Value, resolved.Value, closed.Value, canceled.Value));
    }

    private async Task<Result<StatusRef>> Resolve(ProductStatusAlias alias, CancellationToken cancellationToken)
    {
        var status = await _statusResolver.ForAlias(
            ProductWorkflowOwners.Incident.Key,
            scopeId: null,
            (int)alias,
            cancellationToken);

        if (status.IsFailure)
        {
            _logger.LogError(
                "Unable to resolve the {Alias} incident status. Error message: {Error}",
                alias,
                status.Error);
        }

        return status;
    }

    private async Task<Result<Dictionary<string, Guid>>> ResolveLevels(CancellationToken cancellationToken)
    {
        var selectedSetId = await _productManagementDbContext.IncidentSeveritySetAssignments
            .AsNoTracking()
            .Select(a => (Guid?)a.SeveritySetId)
            .SingleOrDefaultAsync(cancellationToken);

        if (selectedSetId is null)
        {
            return Result.Failure<Dictionary<string, Guid>>("No incident severity set is selected.");
        }

        var levels = await _productManagementDbContext.IncidentSeverityLevels
            .AsNoTracking()
            .Where(l => l.SetId == selectedSetId)
            .Select(l => new { l.Id, l.Name })
            .ToListAsync(cancellationToken);

        return Result.Success(levels.ToDictionary(l => l.Name, l => l.Id, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<HashSet<Guid>> ResolveEmployeeIds(
        ImportPassContext<ImportIncidentDto> context,
        CancellationToken cancellationToken)
    {
        var wanted = context.Accepted
            .SelectMany(r => new Guid?[] { r.Data.CommanderEmployeeId, r.Data.ReportedByEmployeeId })
            .Where(id => id is not null && id != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var found = await _productManagementDbContext.Employees
            .AsNoTracking()
            .Where(e => wanted.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }

    private async Task<HashSet<Guid>> ResolveProductIds(
        ImportPassContext<ImportIncidentDto> context,
        CancellationToken cancellationToken)
    {
        var wanted = context.Accepted
            .SelectMany(r => r.Data.ProductIds)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var found = await _productManagementDbContext.Products
            .AsNoTracking()
            .Where(p => wanted.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }

    private async Task<HashSet<Guid>> ResolveReleaseIds(
        ImportPassContext<ImportIncidentDto> context,
        CancellationToken cancellationToken)
    {
        var wanted = context.Accepted
            .SelectMany(r => r.Data.ReleaseIds)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var found = await _productManagementDbContext.Releases
            .AsNoTracking()
            .Where(r => wanted.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }

    private static Guid? Missing(IReadOnlyList<Guid> wanted, HashSet<Guid> found)
    {
        foreach (var id in wanted)
        {
            if (id != Guid.Empty && !found.Contains(id))
            {
                return id;
            }
        }

        return null;
    }

    private sealed record IncidentStatuses(
        StatusRef Open,
        StatusRef Responding,
        StatusRef Resolved,
        StatusRef Closed,
        StatusRef Canceled);
}
