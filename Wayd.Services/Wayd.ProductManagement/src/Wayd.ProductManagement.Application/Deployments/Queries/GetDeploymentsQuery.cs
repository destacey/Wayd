using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Deployments.Dtos;

namespace Wayd.ProductManagement.Application.Deployments.Queries;

/// <summary>
/// Deployments, most recently started first.
/// </summary>
/// <param name="EnvironmentCategory">
/// Narrows to one kind of target. Filtering on the frozen category rather than joining the environment
/// is what keeps a reclassified environment from changing what past deployments count as.
/// </param>
public sealed record GetDeploymentsQuery(
    Guid? ReleaseId = null,
    Guid? PackageId = null,
    Guid? EnvironmentId = null,
    EnvironmentCategory? EnvironmentCategory = null,
    Instant? StartedOnOrAfter = null) : IQuery<IReadOnlyCollection<DeploymentDto>>;

public sealed class GetDeploymentsQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetDeploymentsQuery, IReadOnlyCollection<DeploymentDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<DeploymentDto>> Handle(
        GetDeploymentsQuery query, CancellationToken cancellationToken)
    {
        var deployments = _productManagementDbContext.Deployments.AsNoTracking();

        if (query.ReleaseId is not null)
        {
            deployments = deployments.Where(d => d.ReleaseId == query.ReleaseId);
        }

        if (query.PackageId is not null)
        {
            deployments = deployments.Where(d => d.PackageId == query.PackageId);
        }

        if (query.EnvironmentId is not null)
        {
            deployments = deployments.Where(d => d.EnvironmentId == query.EnvironmentId);
        }

        if (query.EnvironmentCategory is not null)
        {
            deployments = deployments.Where(d => d.EnvironmentCategory == query.EnvironmentCategory);
        }

        if (query.StartedOnOrAfter is not null)
        {
            deployments = deployments.Where(d => d.StartedAt >= query.StartedOnOrAfter);
        }

        return await Project(deployments, _productManagementDbContext)
            .OrderByDescending(d => d.StartedAt)
            .ToListAsync(cancellationToken);
    }

    internal static IQueryable<DeploymentDto> Project(
        IQueryable<Domain.Models.Deployment> deployments, IProductManagementDbContext dbContext) =>
        deployments.Select(d => new DeploymentDto
        {
            Id = d.Id,
            Key = d.Key,
            ReleaseId = d.ReleaseId,
            ReleaseVersion = dbContext.Releases
                .Where(r => r.Id == d.ReleaseId)
                .Select(r => r.Version)
                .FirstOrDefault(),
            PackageId = d.PackageId,
            PackageVersion = dbContext.ReleasePackages
                .Where(p => p.Id == d.PackageId)
                .Select(p => p.Version)
                .FirstOrDefault(),
            EnvironmentId = d.EnvironmentId,
            EnvironmentName = dbContext.DeploymentEnvironments
                .Where(e => e.Id == d.EnvironmentId)
                .Select(e => e.Name)
                .FirstOrDefault()!,
            EnvironmentCategory = d.EnvironmentCategory,
            ArtifactId = d.ArtifactId,
            StartedAt = d.StartedAt,
            CompletedAt = d.CompletedAt,
            Reason = d.Reason,
            StatusId = d.StatusId,
            StatusName = d.StatusName,
            StatusCategory = d.StatusCategory,
            // Outcome, IsComplete and IsChangeFailure are computed on the aggregate and Ignore()d on the
            // model, so the projection recomputes them from real columns rather than reading them.
            Outcome = (ProductStatusAlias)EF.Property<int>(d, "StatusAliasValue"),
            IsComplete = d.CompletedAt != null,
            IsChangeFailure =
                d.EnvironmentCategory == Common.Domain.Enums.ProductManagement.EnvironmentCategory.Production
                && (EF.Property<int>(d, "StatusAliasValue") == (int)ProductStatusAlias.Failed
                    || EF.Property<int>(d, "StatusAliasValue") == (int)ProductStatusAlias.RolledBack),
        });
}
