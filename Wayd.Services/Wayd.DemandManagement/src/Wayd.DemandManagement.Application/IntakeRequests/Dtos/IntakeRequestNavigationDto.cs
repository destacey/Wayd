namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// Another intake request, named enough to link to: its number and title, both summary tier.
/// </summary>
public sealed record IntakeRequestNavigationDto
{
    /// <summary>The request's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The request's number, which its page is addressed by.</summary>
    public int Key { get; init; }

    /// <summary>What it asks for, in a line.</summary>
    public required string Title { get; init; }
}
