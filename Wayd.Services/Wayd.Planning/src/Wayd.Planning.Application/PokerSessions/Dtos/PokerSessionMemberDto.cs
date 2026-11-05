using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Employees;
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
    /// Whether the member votes. A voting member counts toward a round's voted and waiting totals; one who is not
    /// voting is left out of them but still takes part.
    /// </summary>
    public bool IsVoting { get; set; }

    /// <summary>
    /// When the member first joined.
    /// </summary>
    public Instant JoinedOn { get; set; }

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
        IsVoting = member.IsVoting,
        JoinedOn = member.JoinedOn,
    };
}
