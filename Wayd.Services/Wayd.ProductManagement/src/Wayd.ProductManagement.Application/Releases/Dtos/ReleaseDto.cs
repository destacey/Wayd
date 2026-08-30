using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Application.Releases.Dtos;

/// <summary>
/// A release of one product.
/// </summary>
/// <remarks>
/// <see cref="Version"/> is free text and is never parsed. Callers presenting a list order by
/// <see cref="ReleasedDate"/> then <see cref="Sequence"/>, never by the version string.
/// </remarks>
public sealed record ReleaseDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }

    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;

    /// <summary>The version as the organization writes it. Free text, never parsed.</summary>
    public string Version { get; init; } = default!;

    public string? Name { get; init; }
    public string? Notes { get; init; }

    /// <summary>A manual ordering override, used only where chronology misleads.</summary>
    public long? Sequence { get; init; }

    public LocalDate? TargetDate { get; init; }
    public LocalDate? CutDate { get; init; }
    public LocalDate? ReleasedDate { get; init; }

    /// <summary>The package this release shipped inside, or null when it shipped on its own.</summary>
    public Guid? PackageId { get; init; }

    public Guid StatusId { get; init; }
    public string StatusName { get; init; } = default!;
    public StatusCategory StatusCategory { get; init; }
    public ProductStatusAlias StatusAlias { get; init; }
}
