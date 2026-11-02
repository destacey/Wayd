using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Extensions;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

public sealed record PokerSessionListDto : IMapFrom<PokerSession>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }

    /// <summary>
    /// Public: anyone with the poker View permission can see and join the session. Private: only the people on it.
    /// </summary>
    public Visibility Visibility { get; set; }

    /// <summary>
    /// The employees who run the session.
    /// </summary>
    public List<EmployeeNavigationDto> Facilitators { get; set; } = [];

    /// <summary>
    /// The members who joined as a Participant, or switched to one, including those who have since left.
    /// </summary>
    public List<EmployeeNavigationDto> Participants { get; set; } = [];

    public int RoundCount { get; set; }

    /// <summary>
    /// Whether the caller runs the session, as a facilitator or a poker administrator. Always false for a
    /// read-only token.
    /// </summary>
    public bool CanManage { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PokerSession, PokerSessionListDto>()
            .Map(dest => dest.Status, src => src.Status.GetDisplayName())
            .Map(dest => dest.Facilitators, src => src.Facilitators.Select(f => EmployeeNavigationDto.From(f.Employee!)))
            .Map(dest => dest.Participants, src => src.Members
                .Where(m => m.Mode == PokerSessionMemberMode.Participant)
                .OrderBy(m => m.JoinedOn)
                .Select(m => EmployeeNavigationDto.From(m.Employee!)))
            .Ignore(dest => dest.CanManage)
            .Map(dest => dest.RoundCount, src => src.Rounds.Count);
    }
}
