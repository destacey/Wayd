using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public class CreatePokerSessionRequest
{
    public string Name { get; set; } = default!;
    public Guid EstimationScaleId { get; set; }

    /// <summary>
    /// `Public` (the default): anyone with the poker View permission can see and join the session. `Private`:
    /// only its facilitators, invited employees and the members of invited teams.
    /// </summary>
    public Visibility Visibility { get; set; } = Visibility.Public;

    /// <summary>
    /// Employees who facilitate alongside the caller, who is always the first facilitator.
    /// </summary>
    public List<Guid>? FacilitatorIds { get; set; }

    /// <summary>
    /// Employees invited to the session, who can join it while it is private.
    /// </summary>
    public List<Guid>? InvitedEmployeeIds { get; set; }

    /// <summary>
    /// Teams invited to the session, whose current members can join it while it is private.
    /// </summary>
    public List<Guid>? InvitedTeamIds { get; set; }

    public CreatePokerSessionCommand ToCreatePokerSessionCommand()
    {
        return new CreatePokerSessionCommand(Name, EstimationScaleId, Visibility, FacilitatorIds, InvitedEmployeeIds, InvitedTeamIds);
    }
}

public sealed class CreatePokerSessionRequestValidator : CustomValidator<CreatePokerSessionRequest>
{
    public CreatePokerSessionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(256);
        RuleFor(r => r.EstimationScaleId).NotEmpty();
        RuleFor(r => r.Visibility).IsInEnum();
    }
}
