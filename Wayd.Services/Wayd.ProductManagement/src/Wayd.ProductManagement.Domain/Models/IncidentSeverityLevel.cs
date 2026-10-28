using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One ordered step on an incident severity scale — SEV-1, High, and so on.
/// </summary>
/// <remarks>
/// Levels keep stable ids so an incident can hold the level it was given when the organization
/// later selects a different scale. Risk exposure (impact × likelihood) is a separate idea.
/// </remarks>
public sealed class IncidentSeverityLevel : BaseAuditableEntity
{
    private IncidentSeverityLevel() { }

    internal IncidentSeverityLevel(Guid setId, string name, int sortOrder)
    {
        SetId = setId;
        Name = name;
        SortOrder = sortOrder;
    }

    /// <summary>The scale this level belongs to.</summary>
    public Guid SetId { get; private init; }

    /// <summary>The label shown when opening or editing an incident.</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// Position within the scale. Lower numbers are more severe and sort first.
    /// </summary>
    public int SortOrder { get; private set; }

    internal void Rename(string name)
    {
        Name = name;
    }

    internal void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }
}
