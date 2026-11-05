using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public sealed class SetPokerSessionFacilitatorsRequest
{
    /// <summary>
    /// The employees who facilitate the session from now on, replacing the current list. At least one.
    /// </summary>
    public List<Guid> EmployeeIds { get; set; } = [];

    public SetPokerSessionFacilitatorsCommand ToSetPokerSessionFacilitatorsCommand(Guid id) => new(id, EmployeeIds);
}

public sealed class SetPokerSessionFacilitatorsRequestValidator : CustomValidator<SetPokerSessionFacilitatorsRequest>
{
    public SetPokerSessionFacilitatorsRequestValidator()
    {
        RuleFor(r => r.EmployeeIds)
            .NotEmpty()
            .WithMessage("A poker session needs at least one facilitator.");
    }
}
