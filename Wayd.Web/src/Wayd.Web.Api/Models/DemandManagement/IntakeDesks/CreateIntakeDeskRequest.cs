using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// A new intake desk and its initial reviewers.
/// </summary>
public sealed record CreateIntakeDeskRequest
{
    /// <summary>
    /// What reviewers and administrators call the desk. Unique.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The requester-facing description of what kind of ask to bring here, which requesters choose by.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// How much of a request on the desk reaches people outside it: Open, SummaryOnly or Private.
    /// </summary>
    public IntakeDeskVisibility Visibility { get; set; }

    /// <summary>
    /// Whether a requester can pick the desk. One they cannot is a destination reviewers route to.
    /// </summary>
    public bool IsRequesterSelectable { get; set; } = true;

    /// <summary>
    /// Whether the desk accepts new requests.
    /// </summary>
    public bool IsOpen { get; set; } = true;

    /// <summary>
    /// On a private desk, whether the requester may add participants themselves. Reviewers always can.
    /// </summary>
    public bool RequesterMayAddParticipants { get; set; } = true;

    /// <summary>
    /// The employees named as the desk's reviewers.
    /// </summary>
    public List<Guid> ReviewerIds { get; set; } = [];

    public CreateIntakeDeskCommand ToCreateIntakeDeskCommand() =>
        new(Name, Description, Visibility, IsRequesterSelectable, IsOpen, RequesterMayAddParticipants, ReviewerIds);
}

public sealed class CreateIntakeDeskRequestValidator : CustomValidator<CreateIntakeDeskRequest>
{
    public CreateIntakeDeskRequestValidator()
    {
        RuleFor(d => d.Name).NotEmpty().MaximumLength(128);
        RuleFor(d => d.Description).NotEmpty().MaximumLength(1024);
        RuleFor(d => d.Visibility).IsInEnum();
        RuleFor(d => d.ReviewerIds).NotNull();
        RuleForEach(d => d.ReviewerIds).NotEmpty();
    }
}
