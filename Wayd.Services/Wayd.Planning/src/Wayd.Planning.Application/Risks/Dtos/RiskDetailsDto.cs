using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Models;

namespace Wayd.Planning.Application.Risks.Dtos;

public class RiskDetailsDto : IMapFrom<Risk>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Summary { get; set; }
    public string? Description { get; set; }
    public PlanningTeamNavigationDto? Team { get; set; }
    public Instant ReportedOn { get; set; }
    public required NavigationDto ReportedBy { get; set; }
    public required SimpleNavigationDto Status { get; set; }
    public required SimpleNavigationDto Category { get; set; }
    public required SimpleNavigationDto Impact { get; set; }
    public required SimpleNavigationDto Likelihood { get; set; }
    public required SimpleNavigationDto Exposure { get; set; }
    public NavigationDto? Assignee { get; set; }
    public LocalDate? FollowUpDate { get; set; }
    public string? Response { get; set; }
    public Instant? ClosedDate { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Risk, RiskDetailsDto>()
            .Map(dest => dest.ReportedBy, src => NavigationDto.Create(src.ReportedBy.Id, src.ReportedBy.Key, src.ReportedBy.Name.DisplayName))
            .Map(dest => dest.Status, src => SimpleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.Category, src => SimpleNavigationDto.FromEnum(src.Category))
            .Map(dest => dest.Impact, src => SimpleNavigationDto.FromEnum(src.Impact))
            .Map(dest => dest.Likelihood, src => SimpleNavigationDto.FromEnum(src.Likelihood))
            .Map(dest => dest.Exposure, src => SimpleNavigationDto.FromEnum(src.Exposure))
            .Map(dest => dest.Assignee, src => src.Assignee == null ? null : NavigationDto.Create(src.Assignee.Id, src.Assignee.Key, src.Assignee.Name.DisplayName));
    }
}
