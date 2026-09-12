using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Imports;

/// <summary>
/// Imports deployment environments, the reference data the deployments import resolves against by name.
/// </summary>
/// <remarks>
/// Atomic. Environment names are the natural key the deployments import resolves against, so a
/// half-applied file would leave that import silently resolving some names and rejecting others.
/// </remarks>
public sealed class DeploymentEnvironmentImportDefinition(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportDeploymentEnvironmentDto>(serializer)
{
    public const string ImportKey = "product-management.deployment-environments";

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public override string Key => ImportKey;
    public override string DisplayName => "Deployment Environments";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.DeploymentEnvironments;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportDeploymentEnvironmentDto>> Steps =>
    [
        new("CreateEnvironments", ImportPassScope.Chunked, CreateEnvironments),
    ];

    /// <summary>
    /// Rejects a row whose name is already taken, then creates what is left.
    /// </summary>
    /// <remarks>
    /// Uniqueness <em>within</em> the file is the submission command's rule, since it is a property of
    /// the file rather than of any one row.
    /// </remarks>
    private async Task<Result> CreateEnvironments(
        ImportPassContext<ImportDeploymentEnvironmentDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person defined every
        // environment by hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        var names = context.Rows.Select(r => Normalize(r.Data.Name)).ToList();

        var takenNames = (await _productManagementDbContext.DeploymentEnvironments
                .AsNoTracking()
                .Where(e => names.Contains(e.Name))
                .Select(e => e.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in context.Accepted)
        {
            var name = Normalize(row.Data.Name);

            if (takenNames.Contains(name))
            {
                row.Failed($"An environment named '{name}' already exists.");
                continue;
            }

            var environment = DeploymentEnvironment.Create(
                name, row.Data.Category, row.Data.RingOrder, actor, timestamp);

            // Retired through the real transition rather than created inactive, so the retirement is
            // recorded the way one made by hand would be.
            if (!row.Data.IsActive)
            {
                var retired = environment.Deactivate(actor, timestamp);
                if (retired.IsFailure)
                {
                    row.Failed($"Could not retire environment '{name}': {retired.Error}");
                    continue;
                }
            }

            await _productManagementDbContext.DeploymentEnvironments.AddAsync(environment, cancellationToken);
            row.Created(environment.Id);
        }

        return Result.Success();
    }

    private static string Normalize(string name) => name.Trim();
}
