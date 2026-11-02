using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public sealed class SetPokerSessionInviteesRequest
{
    /// <summary>
    /// The employees invited to the session, replacing the current list. Empty removes every employee invite.
    /// </summary>
    public List<Guid> EmployeeIds { get; set; } = [];

    /// <summary>
    /// The teams invited to the session, replacing the current list. Empty removes every team invite.
    /// </summary>
    public List<Guid> TeamIds { get; set; } = [];

    public SetPokerSessionInviteesCommand ToSetPokerSessionInviteesCommand(Guid id) => new(id, EmployeeIds, TeamIds);
}

public sealed class SetPokerSessionInviteesRequestValidator : CustomValidator<SetPokerSessionInviteesRequest>
{
    public SetPokerSessionInviteesRequestValidator()
    {
        RuleFor(r => r.EmployeeIds).NotNull();
        RuleFor(r => r.TeamIds).NotNull();
    }
}
