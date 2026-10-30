using Wayd.Common.Domain.Discussions;

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

    /// <summary>
    /// A reply to post in the request's conversation with the move, such as a question for the requester when moving to
    /// Waiting on Requester. Saved with the move, and shown with it on the message. Null for none.
    /// </summary>
    public IntakeRequestReplyRequest? Reply { get; set; }
}

/// <summary>A top-level reply in an intake request's conversation, which the requester and participants read.</summary>
public sealed record IntakeRequestReplyRequest
{
    /// <summary>An optional subject, up to 200 characters.</summary>
    public string? Subject { get; set; }

    /// <summary>The reply as markdown, mentions written <c>@[Name](employee:{id})</c>.</summary>
    public required string Body { get; set; }
}

public sealed class ChangeIntakeRequestStatusRequestValidator : CustomValidator<ChangeIntakeRequestStatusRequest>
{
    public ChangeIntakeRequestStatusRequestValidator()
    {
        RuleFor(r => r.StatusId).NotEmpty();
        RuleFor(r => r.Reason).MaximumLength(1024);
        RuleFor(r => r.DuplicateOfId).NotEqual(Guid.Empty);
        When(r => r.Reply is not null, () =>
        {
            RuleFor(r => r.Reply!.Subject).MaximumLength(DiscussionMessage.SubjectMaxLength);
            RuleFor(r => r.Reply!.Body).NotEmpty().MaximumLength(DiscussionMessage.BodyMaxLength);
        });
    }
}
