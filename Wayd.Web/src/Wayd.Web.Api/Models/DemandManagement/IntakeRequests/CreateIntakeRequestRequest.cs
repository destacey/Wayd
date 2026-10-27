using Wayd.DemandManagement.Application.IntakeRequests.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// A request submitted to a desk, for the caller or on someone else's behalf.
/// </summary>
public sealed record CreateIntakeRequestRequest
{
    /// <summary>
    /// What is being asked for, in a line. On most desks everyone who can view requests reads the title, even when the detail stays with the desk.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// The ask in full.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// What gets better, and for whom, if the ask is delivered.
    /// </summary>
    public string? ExpectedBenefits { get; set; }

    /// <summary>
    /// The desk to submit to: one that is open and that requesters can pick.
    /// </summary>
    public Guid DeskId { get; set; }

    /// <summary>
    /// Where the request came from: a channel the desk offers (see the channels lookup with `deskId`). Required
    /// when the desk offers any channel; leave it out for a desk that offers none.
    /// </summary>
    public Guid? ChannelId { get; set; }

    /// <summary>
    /// The employee the request is for, when it is not the caller. The caller is recorded as the submitter.
    /// </summary>
    public Guid? RequesterId { get; set; }

    /// <summary>
    /// When the request has to land, and why.
    /// </summary>
    public IntakeRequestNeedByRequest? NeedBy { get; set; }

    public CreateIntakeRequestCommand ToCreateIntakeRequestCommand() =>
        new(Title, Description, ExpectedBenefits, DeskId, ChannelId, RequesterId, NeedBy?.ToInput());
}

public sealed class CreateIntakeRequestRequestValidator : CustomValidator<CreateIntakeRequestRequest>
{
    public CreateIntakeRequestRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).NotEmpty();
        RuleFor(r => r.DeskId).NotEmpty();
        RuleFor(r => r.ChannelId).NotEqual(Guid.Empty);
        RuleFor(r => r.RequesterId).NotEqual(Guid.Empty);
        RuleFor(r => r.NeedBy!).SetValidator(new IntakeRequestNeedByRequestValidator()).When(r => r.NeedBy is not null);
    }
}
