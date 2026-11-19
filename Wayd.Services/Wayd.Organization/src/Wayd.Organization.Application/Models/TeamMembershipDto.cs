using NodaTime;

namespace Wayd.Organization.Application.Models;

public record TeamMembershipDto
{
    public Guid Id { get; set; }
    public required TeamNavigationDto Child { get; set; }
    public required TeamNavigationDto Parent { get; set; }
    public LocalDate Start { get; set; }
    public LocalDate? End { get; set; }
    public required string State { get; set; }

    // TODO: do this with Mapster
    public static TeamMembershipDto Create(TeamMembership membership, LocalDate today)
    {
        return new TeamMembershipDto()
        {
            Id = membership.Id,
            Child = TeamNavigationDto.FromBaseTeam(membership.Source),
            Parent = TeamNavigationDto.FromBaseTeam(membership.Target),
            Start = membership.DateRange.Start,
            End = membership.DateRange.End,
            State = membership.StateOn(today).GetDisplayName()
        };
    }
}
