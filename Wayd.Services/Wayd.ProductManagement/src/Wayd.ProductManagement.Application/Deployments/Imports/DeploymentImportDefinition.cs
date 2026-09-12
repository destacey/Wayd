using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Deployments.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Deployments.Imports;

/// <summary>
/// Imports deployments, each started against its version or package and then walked to the outcome its
/// row describes by replaying the real transitions.
/// </summary>
/// <remarks>
/// There is no status column. A row with no outcome is in flight; one with an outcome is succeeded,
/// failed, or succeeded and then rolled back, with its real timestamps. Replaying the transitions rather
/// than assigning a status is what gives an imported deployment the same status history a hand-recorded
/// one would have, and what keeps the rollback rule — only a succeeded deployment can be rolled back —
/// in the domain rather than duplicated here.
/// <para>
/// A deployment has no natural key: two builds of one version reaching one environment are two
/// deployments. The import is therefore additive, and re-running a file records everything in it again.
/// </para>
/// <para>
/// Atomic, and a single pass: every row is created and walked before anything is saved, so a row is
/// either wholly applied or not at all.
/// </para>
/// </remarks>
public sealed class DeploymentImportDefinition(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<DeploymentImportDefinition> logger,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportDeploymentDto>(serializer)
{
    public const string ImportKey = "product-management.deployments";

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeploymentImportDefinition> _logger = logger;

    public override string Key => ImportKey;
    public override string DisplayName => "Deployments";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Delivery;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportDeploymentDto>> Steps =>
    [
        new("CreateDeployments", ImportPassScope.Chunked, CreateDeployments),
    ];

    private async Task<Result> CreateDeployments(ImportPassContext<ImportDeploymentDto> context, CancellationToken cancellationToken)
    {
        // One import run is one actor: the events say "the import", not "this person recorded every
        // deployment by hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        var environmentsByName = await ResolveEnvironments(context, cancellationToken);
        var versionIds = await ResolveVersionIds(context, cancellationToken);
        var packageIds = await ResolvePackageIds(context, cancellationToken);

        var statuses = await ResolveStatuses(cancellationToken);
        if (statuses.IsFailure)
            return Result.Failure(statuses.Error);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;
            var environmentName = Normalize(data.EnvironmentName);

            if (!environmentsByName.TryGetValue(environmentName, out var environment))
            {
                row.Failed($"No environment was found named '{environmentName}'.");
                continue;
            }

            // A retired environment is refused for a deployment still in flight, as it is when one is
            // started by hand — but a historical backfill routinely records deployments into environments
            // that have since been decommissioned, and those arrive finished.
            if (!environment.IsActive && data.Outcome is null)
            {
                row.Failed($"'{environment.Name}' is retired and cannot be deployed into. Only a deployment that has already finished can name a retired environment.");
                continue;
            }

            if (data.VersionId is not null && !versionIds.Contains(data.VersionId.Value))
            {
                row.Failed($"No version was found with id '{data.VersionId}'.");
                continue;
            }

            if (data.PackageId is not null && !packageIds.Contains(data.PackageId.Value))
            {
                row.Failed($"No release package was found with id '{data.PackageId}'.");
                continue;
            }

            var created = Deployment.Create(
                data.VersionId,
                data.PackageId,
                environment.Id,
                // Frozen from the environment as it stands now. What the environment counted as when a
                // historical deployment actually ran is not recoverable, so the import cannot do better.
                environment.Category,
                data.ArtifactId,
                data.StartedAt,
                statuses.Value.InProgress,
                environment.Name,
                actor,
                data.StartedAt);
            if (created.IsFailure)
            {
                row.Failed($"Could not start the deployment into '{environment.Name}': {created.Error}");
                continue;
            }

            var walked = Walk(created.Value, data, statuses.Value, environment.Name, actor);
            if (walked.IsFailure)
            {
                row.Failed(walked.Error);
                continue;
            }

            await _productManagementDbContext.Deployments.AddAsync(created.Value, cancellationToken);
            row.Created(created.Value.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Walks a freshly started deployment to the outcome its row describes, in the order the events
    /// actually happen.
    /// </summary>
    /// <remarks>
    /// Each step is the same domain method a person's click would reach, so the status history reads as
    /// though the deployment were recorded as it went rather than in one sitting. A rollback goes through
    /// success first because the domain permits it from nowhere else.
    /// </remarks>
    private static Result Walk(
        Deployment deployment,
        ImportDeploymentDto data,
        DeploymentStatuses statuses,
        string environmentName,
        EventActor actor)
    {
        switch (data.Outcome)
        {
            case null:
                return Result.Success();

            case ImportDeploymentOutcome.Succeeded:
                return Describe(
                    deployment.Succeed(data.CompletedAt!.Value, statuses.Succeeded, environmentName, actor, data.CompletedAt!.Value),
                    "succeed", environmentName);

            case ImportDeploymentOutcome.Failed:
                return Describe(
                    deployment.Fail(data.CompletedAt!.Value, data.Reason, statuses.Failed, environmentName, actor, data.CompletedAt!.Value),
                    "fail", environmentName);

            case ImportDeploymentOutcome.RolledBack:
                var succeeded = Describe(
                    deployment.Succeed(data.CompletedAt!.Value, statuses.Succeeded, environmentName, actor, data.CompletedAt!.Value),
                    "succeed", environmentName);
                if (succeeded.IsFailure)
                    return succeeded;

                return Describe(
                    deployment.RollBack(data.RolledBackAt!.Value, data.Reason, statuses.RolledBack, environmentName, actor, data.RolledBackAt!.Value),
                    "roll back", environmentName);

            default:
                return Result.Failure($"'{data.Outcome}' is not an outcome a deployment can have.");
        }
    }

    private static Result Describe(Result step, string verb, string environmentName) =>
        step.IsSuccess
            ? step
            : Result.Failure($"Could not {verb} the deployment into '{environmentName}': {step.Error}");

    /// <summary>Resolves the environments the file names, retired ones included.</summary>
    private async Task<Dictionary<string, ResolvedEnvironment>> ResolveEnvironments(
        ImportPassContext<ImportDeploymentDto> context, CancellationToken cancellationToken)
    {
        var names = context.Rows.Select(r => Normalize(r.Data.EnvironmentName)).Distinct().ToList();

        return (await _productManagementDbContext.DeploymentEnvironments
                .AsNoTracking()
                .Where(e => names.Contains(e.Name))
                .Select(e => new ResolvedEnvironment(e.Id, e.Name, e.Category, e.IsActive))
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.Name, e => e, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HashSet<Guid>> ResolveVersionIds(
        ImportPassContext<ImportDeploymentDto> context, CancellationToken cancellationToken)
    {
        var ids = context.Rows.Where(r => r.Data.VersionId is not null).Select(r => r.Data.VersionId!.Value).Distinct().ToList();

        return ids.Count == 0
            ? []
            : (await _productManagementDbContext.Versions
                .Where(v => ids.Contains(v.Id))
                .Select(v => v.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet();
    }

    private async Task<HashSet<Guid>> ResolvePackageIds(
        ImportPassContext<ImportDeploymentDto> context, CancellationToken cancellationToken)
    {
        var ids = context.Rows.Where(r => r.Data.PackageId is not null).Select(r => r.Data.PackageId!.Value).Distinct().ToList();

        return ids.Count == 0
            ? []
            : (await _productManagementDbContext.ReleasePackages
                .Where(p => ids.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet();
    }

    /// <summary>
    /// Resolves the four statuses a deployment can reach during an import, once for the whole run.
    /// </summary>
    /// <remarks>
    /// By alias rather than by name, as the handlers do: metrics read the alias, so a renamed outcome
    /// still counts.
    /// </remarks>
    private async Task<Result<DeploymentStatuses>> ResolveStatuses(CancellationToken cancellationToken)
    {
        var inProgress = await Resolve(ProductStatusAlias.InProgress, cancellationToken);
        if (inProgress.IsFailure)
            return Result.Failure<DeploymentStatuses>(inProgress.Error);

        var succeeded = await Resolve(ProductStatusAlias.Succeeded, cancellationToken);
        if (succeeded.IsFailure)
            return Result.Failure<DeploymentStatuses>(succeeded.Error);

        var failed = await Resolve(ProductStatusAlias.Failed, cancellationToken);
        if (failed.IsFailure)
            return Result.Failure<DeploymentStatuses>(failed.Error);

        var rolledBack = await Resolve(ProductStatusAlias.RolledBack, cancellationToken);
        if (rolledBack.IsFailure)
            return Result.Failure<DeploymentStatuses>(rolledBack.Error);

        return Result.Success(new DeploymentStatuses(inProgress.Value, succeeded.Value, failed.Value, rolledBack.Value));
    }

    private async Task<Result<StatusRef>> Resolve(ProductStatusAlias alias, CancellationToken cancellationToken)
    {
        // Product Management assigns workflows organization-wide, so the scope is null.
        var status = await _statusResolver.ForAlias(
            ProductWorkflowOwners.Deployment.Key, scopeId: null, (int)alias, cancellationToken);

        if (status.IsFailure)
            _logger.LogError("Unable to resolve the {Alias} deployment status. Error message: {Error}", alias, status.Error);

        return status;
    }

    /// <summary>An environment the file names, with what deciding a row needs from it.</summary>
    private sealed record ResolvedEnvironment(Guid Id, string Name, EnvironmentCategory Category, bool IsActive);

    /// <summary>The statuses an imported deployment can pass through.</summary>
    private sealed record DeploymentStatuses(StatusRef InProgress, StatusRef Succeeded, StatusRef Failed, StatusRef RolledBack);

    private static string Normalize(string value) => value.Trim();
}
