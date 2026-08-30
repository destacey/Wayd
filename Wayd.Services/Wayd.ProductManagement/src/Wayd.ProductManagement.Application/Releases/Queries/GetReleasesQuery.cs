using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Dtos;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// Releases, newest first.
/// </summary>
/// <remarks>
/// Ordered by released date then sequence, with undated (planned) releases first — never by version,
/// which is free text.
/// </remarks>
public sealed record GetReleasesQuery(
    Guid? ProductId = null,
    Guid? PackageId = null,
    IReadOnlyCollection<StatusCategory>? StatusCategories = null) : IQuery<IReadOnlyCollection<ReleaseDto>>;

public sealed class GetReleasesQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetReleasesQuery, IReadOnlyCollection<ReleaseDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<ReleaseDto>> Handle(
        GetReleasesQuery query, CancellationToken cancellationToken)
    {
        var releases = _productManagementDbContext.Releases.AsNoTracking();

        if (query.ProductId is not null)
        {
            releases = releases.Where(r => r.ProductId == query.ProductId);
        }

        if (query.PackageId is not null)
        {
            releases = releases.Where(r => r.PackageId == query.PackageId);
        }

        if (query.StatusCategories is { Count: > 0 })
        {
            releases = releases.Where(r => query.StatusCategories.Contains(r.StatusCategory));
        }

        return await Project(releases, _productManagementDbContext)
            .OrderByDescending(r => r.ReleasedDate == null)
            .ThenByDescending(r => r.ReleasedDate)
            .ThenByDescending(r => r.Sequence)
            .ToListAsync(cancellationToken);
    }

    internal static IQueryable<ReleaseDto> Project(
        IQueryable<Domain.Models.Release> releases, IProductManagementDbContext dbContext) =>
        releases.Select(r => new ReleaseDto
        {
            Id = r.Id,
            Key = r.Key,
            Product = dbContext.Products
                .Where(p => p.Id == r.ProductId)
                .Select(p => new NavigationDto { Id = p.Id, Key = p.Key, Name = p.Name })
                .FirstOrDefault()!,
            Version = r.Version,
            Name = r.Name,
            Notes = r.Notes,
            Sequence = r.Sequence,
            TargetDate = r.TargetDate,
            CutDate = r.CutDate,
            ReleasedDate = r.ReleasedDate,
            Package = dbContext.ReleasePackages
                .Where(p => p.Id == r.PackageId)
                .Select(p => new NavigationDto { Id = p.Id, Key = p.Key, Name = p.Version })
                .FirstOrDefault(),
            Status = new StatusNavigationDto
            {
                Id = r.StatusId,
                Name = r.StatusName,
                Category = r.StatusCategory,
                Alias = r.StatusAliasValue,
            },
        });
}
