namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// The reviewer who owns an intake request.
/// </summary>
public sealed record AssignIntakeRequestReviewerRequest
{
    /// <summary>
    /// A reviewer on the request's desk, or null to unassign it.
    /// </summary>
    public Guid? ReviewerId { get; set; }
}

public sealed class AssignIntakeRequestReviewerRequestValidator : CustomValidator<AssignIntakeRequestReviewerRequest>
{
    public AssignIntakeRequestReviewerRequestValidator()
    {
        RuleFor(r => r.ReviewerId).NotEqual(Guid.Empty);
    }
}
