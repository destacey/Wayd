using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Dtos;

/// <summary>
/// A coordinated shipment of several component releases.
/// </summary>
/// <remarks>
/// The manifest records every component version that shipped, changed and carried forward alike, so a
/// reader can reconstruct exactly what was in the box.
/// </remarks>
public sealed record ReleasePackageDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }

    /// <summary>The package's own version, distinct from any component's. Free text, never parsed.</summary>
    public string Version { get; init; } = default!;

    public string? Name { get; init; }
    public LocalDate? TargetDate { get; init; }
    public LocalDate? ReleasedDate { get; init; }

    /// <summary>The package's current status.</summary>
    public StatusNavigationDto Status { get; init; } = default!;

    public IReadOnlyCollection<ReleasePackageComponentDto> Components { get; init; } = [];

    /// <summary>
    /// Maps a package and its manifest lines, for <c>ProjectToType</c>.
    /// </summary>
    /// <remarks>
    /// Built per call rather than registered globally, because a component's version reads a second set
    /// and the global config has no request-scoped DbContext to close over.
    /// <para>
    /// That version is a subquery permanently, not for want of a navigation: there is deliberately no
    /// foreign key, because a carried-forward line often names a version with no version row in Wayd at
    /// all. A version and a package are both identified by their version string rather than their
    /// optional name.
    /// </para>
    /// </remarks>
    public static TypeAdapterConfig CreateTypeAdapterConfig(IProductManagementDbContext dbContext)
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<ReleasePackageComponent, ReleasePackageComponentDto>()
            .Map(dto => dto.VersionRecord, c => dbContext.Versions
                .Where(version => version.Id == c.VersionId)
                .Select(version => NavigationDto.Create(version.Id, version.Key, version.Number))
                .FirstOrDefault());

        config.NewConfig<ReleasePackage, ReleasePackageDto>()
            .Map(dto => dto.Status, p => new StatusNavigationDto
            {
                Id = p.StatusId,
                Name = p.StatusName,
                Category = p.StatusCategory,
                Alias = p.StatusAliasValue,
            });

        return config;
    }
}

public sealed record ReleasePackageComponentDto
{
    /// <summary>The component this entry is for.</summary>
    public NavigationDto Product { get; init; } = default!;

    /// <summary>
    /// The version record this line came from, where one was recorded. Its <c>Name</c> is that
    /// version's number, which may differ from <see cref="Version"/> if the manifest was hand-authored.
    /// </summary>
    public NavigationDto? VersionRecord { get; init; }

    /// <summary>The component version in this package. Free text, never parsed.</summary>
    public string Version { get; init; } = default!;

    /// <summary>Whether the component changed in this package or was carried forward unchanged.</summary>
    public ManifestEntryKind Kind { get; init; }
}
