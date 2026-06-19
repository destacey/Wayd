using Wayd.Common.Application.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

public sealed record ProjectHealthCheckDetailsDto : IMapFrom<ProjectHealthCheck>
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required SimpleNavigationDto Status { get; set; }
    public required NavigationDto ReportedBy { get; set; }
    public Instant ReportedOn { get; set; }
    public Instant Expiration { get; set; }
    public string? Note { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectHealthCheck, ProjectHealthCheckDetailsDto>()
            .Map(dest => dest.Status, src => SimpleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.ReportedBy, src => NavigationDto.Create(src.ReportedBy.Id, src.ReportedBy.Key, src.ReportedBy.Name.DisplayName));
    }
}
