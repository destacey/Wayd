namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTags;

/// <summary>
/// The tag a free tag's requests move to.
/// </summary>
public sealed record MergeIntakeRequestTagRequest
{
    /// <summary>
    /// Another free tag, or an active tag on an active axis.
    /// </summary>
    public Guid TargetTagId { get; set; }
}

public sealed class MergeIntakeRequestTagRequestValidator : CustomValidator<MergeIntakeRequestTagRequest>
{
    public MergeIntakeRequestTagRequestValidator()
    {
        RuleFor(r => r.TargetTagId).NotEmpty();
    }
}
