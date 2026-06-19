namespace Wayd.Common.Application.Scoring.ScoringModels.Dtos;

/// <summary>
/// The result of evaluating a scoring model against a set of supplied criterion values. Carries every
/// output value (in evaluation order) plus the primary score, so callers can preview formula results.
/// </summary>
public sealed record ScoringModelEvaluationDto
{
    public decimal PrimaryValue { get; set; }
    public required List<ScoringModelOutputValueDto> Outputs { get; set; }
}

/// <summary>
/// A single named output value produced by an evaluation.
/// </summary>
public sealed record ScoringModelOutputValueDto
{
    public required string Token { get; set; }
    public required string Name { get; set; }
    public decimal Value { get; set; }
    public bool IsPrimary { get; set; }
    public int Order { get; set; }
}
