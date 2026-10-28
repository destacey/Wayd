namespace Wayd.DemandManagement.Application.DemandProjects.Dtos;

/// <summary>
/// A PPM project, named enough to link to, as Demand Management's copy holds it.
/// </summary>
public sealed record DemandProjectNavigationDto
{
    /// <summary>The project's id, the same as in PPM.</summary>
    public Guid Id { get; init; }

    /// <summary>The project's key, which its page is addressed by.</summary>
    public required string Key { get; init; }

    /// <summary>The project's name.</summary>
    public required string Name { get; init; }
}
