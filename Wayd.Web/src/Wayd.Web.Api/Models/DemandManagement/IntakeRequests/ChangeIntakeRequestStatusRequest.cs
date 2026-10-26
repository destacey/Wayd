namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// The status to move an intake request to, with what that move needs.
/// </summary>
public sealed record ChangeIntakeRequestStatusRequest
{
    /// <summary>A status of the workflow governing the request's desk, from its status options.</summary>
    public Guid StatusId { get; set; }

    /// <summary>
    /// Why, as the requester reads it. Required for declining, deferring, marking a duplicate and finishing a
    /// request; the status options say which.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>For a status meaning Duplicate, the request this one gives way to; otherwise null.</summary>
    public Guid? DuplicateOfId { get; set; }
}

public sealed class ChangeIntakeRequestStatusRequestValidator : CustomValidator<ChangeIntakeRequestStatusRequest>
{
    public ChangeIntakeRequestStatusRequestValidator()
    {
        RuleFor(r => r.StatusId).NotEmpty();
        RuleFor(r => r.Reason).MaximumLength(1024);
        RuleFor(r => r.DuplicateOfId).NotEqual(Guid.Empty);
    }
}
