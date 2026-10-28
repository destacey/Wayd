namespace Wayd.DemandManagement.Application.IntakeDesks.Dtos;

/// <summary>
/// An active scoring model a desk can score its requests with.
/// </summary>
public sealed record IntakeScoringModelChoiceDto
{
    /// <summary>The model's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The model's key.</summary>
    public int Key { get; init; }

    /// <summary>The model's name.</summary>
    public required string Name { get; init; }

    /// <summary>What the model is for, as its administrator described it.</summary>
    public required string Description { get; init; }
}
