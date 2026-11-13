using Wayd.DemandManagement.Application.IntakeDesks.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// A planning poker session to start on a desk, with the requests it will estimate.
/// </summary>
public sealed record StartIntakeDeskPokerSessionRequest
{
    /// <summary>The session's name, at most 256 characters.</summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The ids of open requests on the desk to estimate, one round each, in this order. May be empty; requests can
    /// be added to the session later.
    /// </summary>
    public List<Guid> RequestIds { get; set; } = [];

    /// <summary>
    /// The ids of employees invited to vote, beside the desk's agents and Desk Admins, who all facilitate the
    /// session.
    /// </summary>
    public List<Guid>? InvitedEmployeeIds { get; set; }

    public StartIntakeDeskPokerSessionCommand ToStartIntakeDeskPokerSessionCommand(Guid deskId) =>
        new(deskId, Name, RequestIds, InvitedEmployeeIds);
}

public sealed class StartIntakeDeskPokerSessionRequestValidator : CustomValidator<StartIntakeDeskPokerSessionRequest>
{
    public StartIntakeDeskPokerSessionRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(256);
        RuleFor(r => r.RequestIds).NotNull();
        RuleForEach(r => r.RequestIds).NotEmpty();
    }
}
