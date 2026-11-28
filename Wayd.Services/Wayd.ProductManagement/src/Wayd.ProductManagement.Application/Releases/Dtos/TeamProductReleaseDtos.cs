using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.ProductManagement.Application.Releases.Dtos;

/// <summary>
/// The releases and versions still to ship on the Proposed or Active products the caller's teams are linked to:
/// those targeted within the window, and those already past their target date.
/// </summary>
public sealed record TeamProductReleasesDto
{
    /// <summary>
    /// Releases in a Proposed or Active status with a target date on or before the window's last day, earliest target
    /// first. Null when the caller may not view releases.
    /// </summary>
    public IReadOnlyList<TeamProductReleaseDto>? Releases { get; init; }

    /// <summary>
    /// Versions in a Proposed or Active status with a target date on or before the window's last day, earliest target
    /// first. Null when the caller may not view delivery.
    /// </summary>
    public IReadOnlyList<TeamProductVersionDto>? Versions { get; init; }
}

/// <summary>A release still to be announced that reaches one of the caller's teams' products.</summary>
public sealed record TeamProductReleaseDto
{
    /// <summary>Release id.</summary>
    public Guid Id { get; init; }

    /// <summary>Release key. The record is addressed by this key.</summary>
    public int Key { get; init; }

    /// <summary>The release's own label, such as <c>2026.07</c>. Free text, never parsed.</summary>
    public string Version { get; init; } = default!;

    /// <summary>The release's name, where it has one.</summary>
    public string? Name { get; init; }

    /// <summary>When the release is expected to be announced.</summary>
    public LocalDate TargetDate { get; init; }

    /// <summary>Whether the target date is before today in the system time zone.</summary>
    public bool IsPastTarget { get; init; }

    /// <summary>Where the release is in its workflow: a status in the Proposed or Active category.</summary>
    public StatusNavigationDto Status { get; init; } = default!;

    /// <summary>
    /// The product or product line the release is announced under, or null for a release spanning product lines.
    /// </summary>
    public NavigationDto? Product { get; init; }

    /// <summary>
    /// The caller's teams' products the release reaches, by name: the product it is announced under, the products
    /// beneath that product line, and the products of the versions it carries directly or in a package as changed.
    /// </summary>
    public IReadOnlyList<NavigationDto> TeamProducts { get; init; } = [];
}

/// <summary>A version still to ship, cut against one of the caller's teams' products.</summary>
public sealed record TeamProductVersionDto
{
    /// <summary>Version id.</summary>
    public Guid Id { get; init; }

    /// <summary>Version key. The record is addressed by this key.</summary>
    public int Key { get; init; }

    /// <summary>The version as the organization writes it, such as <c>4.12.0</c>. Free text, never parsed.</summary>
    public string Number { get; init; } = default!;

    /// <summary>The version's name, where it has one.</summary>
    public string? Name { get; init; }

    /// <summary>When the version is expected to ship.</summary>
    public LocalDate TargetDate { get; init; }

    /// <summary>Whether the target date is before today in the system time zone.</summary>
    public bool IsPastTarget { get; init; }

    /// <summary>Where the version is in its workflow: a status in the Proposed or Active category.</summary>
    public StatusNavigationDto Status { get; init; } = default!;

    /// <summary>The product the version is cut against.</summary>
    public NavigationDto Product { get; init; } = default!;
}
