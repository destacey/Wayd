using Wayd.DemandManagement.Application.IntakeRequests.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// An intake request's editable fields, overwritten as a whole. Fields that did not change record nothing.
/// </summary>
public sealed record UpdateIntakeRequestRequest
{
    /// <summary>
    /// The request's id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// What is being asked for, in a line.
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
    /// Where the request came from: a channel the request's desk offers, or the one it already has. Null keeps a
    /// request with no channel without one, and clears a channel only while the desk offers none.
    /// </summary>
    public Guid? ChannelId { get; set; }

    /// <summary>
    /// When the request has to land and why. Null clears the date, its basis and its reference together.
    /// </summary>
    public IntakeRequestNeedByRequest? NeedBy { get; set; }

    public UpdateIntakeRequestCommand ToUpdateIntakeRequestCommand() =>
        new(Id, Title, Description, ExpectedBenefits, ChannelId, NeedBy?.ToInput());
}

public sealed class UpdateIntakeRequestRequestValidator : CustomValidator<UpdateIntakeRequestRequest>
{
    public UpdateIntakeRequestRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
        RuleFor(r => r.Title).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).NotEmpty();
        RuleFor(r => r.ChannelId).NotEqual(Guid.Empty);
        RuleFor(r => r.NeedBy!).SetValidator(new IntakeRequestNeedByRequestValidator()).When(r => r.NeedBy is not null);
    }
}
