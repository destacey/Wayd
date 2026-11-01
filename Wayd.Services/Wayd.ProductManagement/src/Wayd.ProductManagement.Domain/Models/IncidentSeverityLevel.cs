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

    internal IncidentSeverityLevel(Guid setId, string name, int sortOrder, string color)
    {
        SetId = setId;
        Name = name;
        SortOrder = sortOrder;
        Color = color;
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

    /// <summary>
    /// Accent color for this rank, stored as a hex code (e.g. "#E07A4C").
    /// </summary>
    public string Color
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Color)).Trim();
    } = default!;

    /// <summary>Default accent for a rank when none is supplied (most severe first).</summary>
    public static string DefaultColorForSortOrder(int sortOrder) =>
        sortOrder switch
        {
            0 => "#E07A4C",
            1 => "#D4A017",
            2 => "#7FA3BF",
            _ => "#8E9E98",
        };

    internal void Rename(string name)
    {
        Name = name;
    }

    internal void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    internal void SetColor(string color)
    {
        Color = color;
    }
}
