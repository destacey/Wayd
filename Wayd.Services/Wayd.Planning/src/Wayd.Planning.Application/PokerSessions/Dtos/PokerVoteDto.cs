using Wayd.Common.Application.Identity.Users;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

public sealed record PokerVoteDto : IMapFrom<PokerVote>
{
    public Guid Id { get; set; }
    public UserNavigationDto? Participant { get; set; }
    public string Value { get; set; } = default!;
    public Instant SubmittedOn { get; set; }
}
