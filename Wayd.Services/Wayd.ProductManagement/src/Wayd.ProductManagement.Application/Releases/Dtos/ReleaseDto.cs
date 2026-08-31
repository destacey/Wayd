using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Dtos;

/// <summary>
/// A release of one product.
/// </summary>
/// <remarks>
/// <see cref="Version"/> is free text and is never parsed. Callers presenting a list order by
/// <see cref="ReleasedDate"/> then <see cref="Sequence"/>, never by the version string.
/// </remarks>
public sealed record ReleaseDto : IMapFrom<Release>
{
    public Guid Id { get; init; }
    public int Key { get; init; }

    /// <summary>The product this release is for.</summary>
    public NavigationDto Product { get; init; } = default!;

    /// <summary>The version as the organization writes it. Free text, never parsed.</summary>
    public string Version { get; init; } = default!;

    public string? Name { get; init; }
    public string? Notes { get; init; }

    /// <summary>A manual ordering override, used only where chronology misleads.</summary>
    public long? Sequence { get; init; }

    public LocalDate? TargetDate { get; init; }
    public LocalDate? CutDate { get; init; }
    public LocalDate? ReleasedDate { get; init; }

    /// <summary>
    /// The package this release shipped inside, or null when it shipped on its own. Its <c>Name</c> is
    /// the package version.
    /// </summary>
    public NavigationDto? Package { get; init; }

    /// <summary>The release's current status.</summary>
    public StatusNavigationDto Status { get; init; } = default!;

    /// <remarks>
    /// <see cref="Product"/> needs no entry: its members are same-named, so convention flattens the
    /// navigation onto the nav DTO. The two below cannot be inferred — a package is identified by its
    /// version rather than its optional name, and the status is flattened across four columns.
    /// </remarks>
    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Release, ReleaseDto>()
            .Map(dest => dest.Package, src => src.Package != null
                ? NavigationDto.Create(src.Package.Id, src.Package.Key, src.Package.Version)
                : null)
            .Map(dest => dest.Status, src => new StatusNavigationDto
            {
                Id = src.StatusId,
                Name = src.StatusName,
                Category = src.StatusCategory,
                Alias = src.StatusAliasValue,
            });
    }
}
