using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Dtos;

/// <summary>
/// A version of one product.
/// </summary>
/// <remarks>
/// <see cref="Number"/> is free text and is never parsed. Callers presenting a list order by
/// <see cref="ReleasedAt"/> then <see cref="Sequence"/>, never by the version string.
/// </remarks>
public sealed record VersionDto
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
    public Instant? CutAt { get; init; }
    public Instant? ReleasedAt { get; init; }

    /// <summary>The version's current status: the organization's name for it, its category and its well-known alias.</summary>
    public StatusNavigationDto Status { get; init; } = default!;

    /// <summary>
    /// When the record entered its current status, or <c>null</c> when no change has been recorded.
    /// </summary>
    /// <remarks>
    /// The instant rather than a duration, so a cached response does not go stale: the time in the
    /// current status is now minus this. Read from the record's latest transition, which its transition
    /// count addresses exactly.
    /// </remarks>
    public Instant? StatusEnteredOn { get; init; }

    /// <summary>
    /// Maps a version, for <c>ProjectToType</c>.
    /// </summary>
    /// <remarks>
    /// Built per call rather than registered globally, because <see cref="StatusEnteredOn"/> reads the
    /// shared transition log and the global config has no request-scoped DbContext to close over.
    /// <para>
    /// <see cref="Product"/> needs no entry: its members are same-named, so convention flattens the
    /// navigation onto the nav DTO. The status cannot be inferred — it is flattened across four columns.
    /// </para>
    /// <para>
    /// There is deliberately no package here. A version carries no foreign key to the package it
    /// shipped in; membership is recorded by the package's manifest, and the packages query resolves it
    /// the other way with <c>containingProductId</c>.
    /// </para>
    /// </remarks>
    public static TypeAdapterConfig CreateTypeAdapterConfig(IStatusWorkflowDbContext statusWorkflowDbContext)
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<Version, VersionDto>()
            .Map(dto => dto.StatusEnteredOn, src => statusWorkflowDbContext.StatusTransitions
                .Where(t => t.OwnerType == ProductWorkflowOwners.Version.Key
                    && t.RecordId == src.Id
                    && t.Sequence == src.StatusTransitionCount - 1)
                .Select(t => (Instant?)t.ChangedOn)
                .FirstOrDefault())
            .Map(dest => dest.Status, src => new StatusNavigationDto
            {
                Id = src.StatusId,
                Name = src.StatusName,
                Category = src.StatusCategory,
                Alias = src.StatusAliasValue,
            });

        return config;
    }
}
