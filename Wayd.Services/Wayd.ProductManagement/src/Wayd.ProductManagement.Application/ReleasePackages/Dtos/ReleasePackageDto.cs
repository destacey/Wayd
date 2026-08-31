using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

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
}

public sealed record ReleasePackageComponentDto
{
    /// <summary>The component this entry is for.</summary>
    public NavigationDto Product { get; init; } = default!;

    /// <summary>
    /// The release this version came from, where one was recorded. Its <c>Name</c> is that release's
    /// version, which may differ from <see cref="Version"/> if the manifest was hand-authored.
    /// </summary>
    public NavigationDto? Release { get; init; }

    /// <summary>The component version in this package. Free text, never parsed.</summary>
    public string Version { get; init; } = default!;

    /// <summary>Whether the component changed in this package or was carried forward unchanged.</summary>
    public ManifestEntryKind Kind { get; init; }
}
