using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;

namespace Wayd.ProductManagement.Application.ReleasePackages.Queries;

/// <summary>
/// Release packages, newest first.
/// </summary>
public sealed record GetReleasePackagesQuery(
    IReadOnlyCollection<StatusCategory>? StatusCategories = null,
    Guid? ContainingProductId = null) : IQuery<IReadOnlyCollection<ReleasePackageDto>>;

public sealed class GetReleasePackagesQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetReleasePackagesQuery, IReadOnlyCollection<ReleasePackageDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<ReleasePackageDto>> Handle(
        GetReleasePackagesQuery query, CancellationToken cancellationToken)
    {
        var packages = _productManagementDbContext.ReleasePackages.AsNoTracking();

        if (query.StatusCategories is { Count: > 0 })
        {
            packages = packages.Where(p => query.StatusCategories.Contains(p.StatusCategory));
        }

        if (query.ContainingProductId is not null)
        {
            packages = packages.Where(p => _productManagementDbContext.ReleasePackageComponents
                .Any(c => c.PackageId == p.Id && c.ProductId == query.ContainingProductId));
        }

        return await Project(packages, _productManagementDbContext)
            .OrderByDescending(p => p.ReleasedDate == null)
            .ThenByDescending(p => p.ReleasedDate)
            .ToListAsync(cancellationToken);
    }

    internal static IQueryable<ReleasePackageDto> Project(
        IQueryable<Domain.Models.ReleasePackage> packages, IProductManagementDbContext dbContext) =>
        packages.Select(p => new ReleasePackageDto
        {
            Id = p.Id,
            Key = p.Key,
            Version = p.Version,
            Name = p.Name,
            TargetDate = p.TargetDate,
            ReleasedDate = p.ReleasedDate,
            Status = new StatusNavigationDto
            {
                Id = p.StatusId,
                Name = p.StatusName,
                Category = p.StatusCategory,
                Alias = p.StatusAliasValue,
            },
            Components = dbContext.ReleasePackageComponents
                .Where(c => c.PackageId == p.Id)
                .Select(c => new ReleasePackageComponentDto
                {
                    Product = dbContext.Products
                        .Where(product => product.Id == c.ProductId)
                        .Select(product => new NavigationDto
                        {
                            Id = product.Id,
                            Key = product.Key,
                            Name = product.Name,
                        })
                        .FirstOrDefault()!,
                    Release = dbContext.Releases
                        .Where(release => release.Id == c.ReleaseId)
                        .Select(release => new NavigationDto
                        {
                            Id = release.Id,
                            Key = release.Key,
                            Name = release.Version,
                        })
                        .FirstOrDefault(),
                    Version = c.Version,
                    Kind = c.Kind,
                })
                .ToList(),
        });
}
