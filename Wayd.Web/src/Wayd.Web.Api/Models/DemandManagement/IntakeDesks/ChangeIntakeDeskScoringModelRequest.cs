namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// The scoring model a desk's requests should be scored with.
/// </summary>
public sealed record ChangeIntakeDeskScoringModelRequest
{
    /// <summary>An active scoring model, from the scoring model choices, or null to stop scoring the desk's requests.</summary>
    public Guid? ScoringModelId { get; set; }
}

public sealed class ChangeIntakeDeskScoringModelRequestValidator : CustomValidator<ChangeIntakeDeskScoringModelRequest>
{
    public ChangeIntakeDeskScoringModelRequestValidator()
    {
        RuleFor(r => r.ScoringModelId).NotEqual(Guid.Empty);
    }
}
