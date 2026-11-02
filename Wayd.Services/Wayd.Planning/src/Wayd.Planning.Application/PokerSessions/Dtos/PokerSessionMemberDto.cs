using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Employees;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

/// <summary>
/// Someone who joined a poker session, and how they take part.
/// </summary>
public sealed record PokerSessionMemberDto : IMapFrom<PokerSessionMember>
{
    /// <summary>
    /// The member. Live presence carries the same employee id, which is how a connected person is matched to
    /// their membership.
    /// </summary>
    public required EmployeeNavigationDto Employee { get; set; }

    /// <summary>
    /// Participant, who votes and counts toward the voted and waiting totals, or Observer, who does neither.
    /// After the member leaves, the mode they left in.
    /// </summary>
    public PokerSessionMemberMode Mode { get; set; }

    /// <summary>
    /// When the member first joined.
    /// </summary>
    public Instant JoinedOn { get; set; }

    /// <summary>
    /// When the member left, or null while they are in the session.
    /// </summary>
    public Instant? LeftOn { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PokerSessionMember, PokerSessionMemberDto>()
            .Map(dest => dest.Employee, src => EmployeeNavigationDto.From(src.Employee!));
    }

    /// <summary>
    /// The member's DTO from an entity whose employee is loaded separately.
    /// </summary>
    public static PokerSessionMemberDto From(PokerSessionMember member, Employee employee) => new()
    {
        Employee = EmployeeNavigationDto.From(employee),
        Mode = member.Mode,
        JoinedOn = member.JoinedOn,
        LeftOn = member.LeftOn,
    };
}
