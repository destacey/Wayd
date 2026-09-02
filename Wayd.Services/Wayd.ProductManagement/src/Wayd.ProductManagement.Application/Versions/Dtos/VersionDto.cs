using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Dtos;

/// <summary>
/// A version of one product.
/// </summary>
/// <remarks>
/// <see cref="Number"/> is free text and is never parsed. Callers presenting a list order by
/// <see cref="ReleasedDate"/> then <see cref="Sequence"/>, never by the version string.
/// </remarks>
public sealed record VersionDto : IMapFrom<Version>
{
    public Guid Id { get; init; }
    public int Key { get; init; }

    /// <summary>The product this version is for.</summary>
    public NavigationDto Product { get; init; } = default!;

    /// <summary>The version as the organization writes it. Free text, never parsed.</summary>
    public string Number { get; init; } = default!;

    public string? Name { get; init; }
    public string? Notes { get; init; }

    /// <summary>A manual ordering override, used only where chronology misleads.</summary>
    public long? Sequence { get; init; }

    public LocalDate? TargetDate { get; init; }
    public LocalDate? CutDate { get; init; }
    public LocalDate? ReleasedDate { get; init; }

    /// <summary>The version's current status.</summary>
    public StatusNavigationDto Status { get; init; } = default!;

    /// <remarks>
    /// <see cref="Product"/> needs no entry: its members are same-named, so convention flattens the
    /// navigation onto the nav DTO. The status cannot be inferred — it is flattened across four columns.
    /// <para>
    /// There is deliberately no package here. A version carries no foreign key to the package it
    /// shipped in; membership is recorded by the package's manifest, and the packages query resolves it
    /// the other way with <c>containingProductId</c>.
    /// </para>
    /// </remarks>
    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Version, VersionDto>()
            .Map(dest => dest.Status, src => new StatusNavigationDto
            {
                Id = src.StatusId,
                Name = src.StatusName,
                Category = src.StatusCategory,
                Alias = src.StatusAliasValue,
            });
    }
}
