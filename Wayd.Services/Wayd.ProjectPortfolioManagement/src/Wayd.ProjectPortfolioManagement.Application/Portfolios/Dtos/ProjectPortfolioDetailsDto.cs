using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;

public sealed record ProjectPortfolioDetailsDto : IMapFrom<ProjectPortfolio>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The unique key of the portfolio.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the portfolio.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// A detailed description of the portfolio’s purpose.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// The status of the portfolio.
    /// </summary>
    public required LifecycleNavigationDto Status { get; set; }

    /// <summary>
    /// The scoring model assigned to the portfolio, or null if scoring is not enabled.
    /// </summary>
    public NavigationDto? ScoringModel { get; set; }

    /// <summary>
    /// The date range defining the portfolio’s lifecycle.
    /// </summary>
    //public FlexibleDateRange? DateRange { get; set; }

    /// <summary>
    /// The sponsors of the portfolio.
    /// </summary>
    public required List<EmployeeNavigationDto> PortfolioSponsors { get; set; } = [];

    /// <summary>
    /// The owners of the portfolio.
    /// </summary>
    public required List<EmployeeNavigationDto> PortfolioOwners { get; set; } = [];

    /// <summary>
    /// The managers of the portfolio.
    /// </summary>
    public required List<EmployeeNavigationDto> PortfolioManagers { get; set; } = [];

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectPortfolio, ProjectPortfolioDetailsDto>()
            .Map(dest => dest.Status, src => LifecycleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.ScoringModel, src => src.ScoringModel == null
                ? null
                : NavigationDto.Create(src.ScoringModel.Id, src.ScoringModel.Key, src.ScoringModel.Name))
            .Map(dest => dest.PortfolioSponsors, src => src.Roles.Where(r => r.Role == ProjectPortfolioRole.Sponsor).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.PortfolioOwners, src => src.Roles.Where(r => r.Role == ProjectPortfolioRole.Owner).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.PortfolioManagers, src => src.Roles.Where(r => r.Role == ProjectPortfolioRole.Manager).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList());
    }
}
