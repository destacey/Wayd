using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Dtos;

/// <summary>
/// What was announced to customers, and the versions and packages that carried it.
/// </summary>
/// <remarks>
/// <see cref="Version"/> is the announcement's own label and is free text, never parsed. Callers
/// presenting a list order by <see cref="ReleasedDate"/> then <see cref="Sequence"/>.
/// </remarks>
public sealed record ReleaseDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }

    /// <summary>
    /// The product this release is announced under, or <c>null</c> where it spans product lines.
    /// </summary>
    public NavigationDto? Product { get; init; }

    /// <summary>The release's own version label. Free text, never parsed.</summary>
    public string Version { get; init; } = default!;

    public string? Name { get; init; }

    /// <summary>Product notes, written for customers.</summary>
    public string? Notes { get; init; }

    /// <summary>A manual ordering override, used only where chronology misleads.</summary>
    public long? Sequence { get; init; }

    public LocalDate? TargetDate { get; init; }
    public LocalDate? ReleasedDate { get; init; }

    /// <summary>The release's current status.</summary>
    public StatusNavigationDto Status { get; init; } = default!;

    /// <summary>The versions this release announces directly, outside any package.</summary>
    public IReadOnlyCollection<ReleaseVersionDto> Versions { get; init; } = [];

    /// <summary>The packages this release shipped.</summary>
    public IReadOnlyCollection<ReleasePackageSummaryDto> Packages { get; init; } = [];

    /// <summary>
    /// Maps a release and its contents, for <c>ProjectToType</c>.
    /// </summary>
    /// <remarks>
    /// Built per call rather than registered globally, because the version and package lines each read
    /// a second set and the global config has no request-scoped DbContext to close over — the same
    /// reason <c>ReleasePackageDto</c> builds its own.
    /// <para>
    /// The contents are read through subqueries rather than navigations so that one projection answers
    /// "what does this release contain" from both sides at once. A join row carries only ids; what a
    /// reader wants is the version number and the package version, which live on the records.
    /// </para>
    /// </remarks>
    public static TypeAdapterConfig CreateTypeAdapterConfig(IProductManagementDbContext dbContext)
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<ReleaseVersion, ReleaseVersionDto>()
            .Map(dto => dto.Version, rv => dbContext.Versions
                .Where(version => version.Id == rv.VersionId)
                .Select(version => NavigationDto.Create(version.Id, version.Key, version.Number))
                .FirstOrDefault())
            .Map(dto => dto.Product, rv => dbContext.Versions
                .Where(version => version.Id == rv.VersionId)
                .Select(version => dbContext.Products
                    .Where(product => product.Id == version.ProductId)
                    .Select(product => NavigationDto.Create(product.Id, product.Key, product.Name))
                    .FirstOrDefault())
                .FirstOrDefault())
            .Map(dto => dto.ReleasedDate, rv => dbContext.Versions
                .Where(version => version.Id == rv.VersionId)
                .Select(version => version.ReleasedDate)
                .FirstOrDefault());

        config.NewConfig<ReleasePackageInclusion, ReleasePackageSummaryDto>()
            .Map(dto => dto.Package, rp => dbContext.ReleasePackages
                .Where(package => package.Id == rp.PackageId)
                .Select(package => NavigationDto.Create(package.Id, package.Key, package.Version))
                .FirstOrDefault())
            .Map(dto => dto.ReleasedDate, rp => dbContext.ReleasePackages
                .Where(package => package.Id == rp.PackageId)
                .Select(package => package.ReleasedDate)
                .FirstOrDefault());

        config.NewConfig<Release, ReleaseDto>()
            .Map(dto => dto.Product, r => r.ProductId == null
                ? null
                : dbContext.Products
                    .Where(product => product.Id == r.ProductId)
                    .Select(product => NavigationDto.Create(product.Id, product.Key, product.Name))
                    .FirstOrDefault())
            .Map(dto => dto.Status, r => new StatusNavigationDto
            {
                Id = r.StatusId,
                Name = r.StatusName,
                Category = r.StatusCategory,
                Alias = r.StatusAliasValue,
            });

        return config;
    }
}

/// <summary>One version a release announces directly.</summary>
public sealed record ReleaseVersionDto
{
    /// <summary>The version record. Its <c>Name</c> is the version number.</summary>
    public NavigationDto Version { get; init; } = default!;

    /// <summary>The product that version was cut against.</summary>
    public NavigationDto? Product { get; init; }

    /// <summary>
    /// When that version shipped, or <c>null</c> while it has not.
    /// </summary>
    /// <remarks>
    /// Carried here so a reader can see at a glance which contents are still outstanding — the same
    /// fact the release's own <c>MarkReleased</c> refuses on.
    /// </remarks>
    public LocalDate? ReleasedDate { get; init; }
}

/// <summary>One package a release shipped.</summary>
public sealed record ReleasePackageSummaryDto
{
    /// <summary>The package. Its <c>Name</c> is the package version.</summary>
    public NavigationDto Package { get; init; } = default!;

    /// <summary>When that package shipped, or <c>null</c> while it has not.</summary>
    public LocalDate? ReleasedDate { get; init; }
}
