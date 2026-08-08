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

    /// <summary>
    /// Whether the current user may manage this portfolio — change its status, edit its details, or assign
    /// its roles. True when the user is an Owner or Manager of the portfolio, or holds the domain-wide PPM
    /// administrator grant. Sponsors are excluded.
    /// <para>
    /// This is a UI hint only; the aggregate enforces the same rule. It is false when the DTO is produced
    /// by the parameterless global mapping, so callers that need it must use
    /// <see cref="CreateTypeAdapterConfig"/>.
    /// </para>
    /// </summary>
    public bool CanManagePortfolio { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        ConfigureCore(config, employeeId: null, isPpmAdministrator: false);
    }

    /// <summary>
    /// Creates a config that maps <see cref="ProjectPortfolio"/> to this DTO including the
    /// <see cref="CanManagePortfolio"/> authorization hint for the given actor.
    /// </summary>
    /// <param name="employeeId">The current user's linked employee, or null when unauthenticated.</param>
    /// <param name="isPpmAdministrator">
    /// Whether the current user holds the domain-wide PPM administrator grant, which substitutes for role
    /// membership. Must mirror <see cref="ProjectPortfolio.CanManagePortfolio"/> — if this hint disagrees
    /// with the aggregate, the UI hides controls the server would accept or vice versa.
    /// </param>
    public static TypeAdapterConfig CreateTypeAdapterConfig(Guid? employeeId, bool isPpmAdministrator)
    {
        var config = new TypeAdapterConfig();
        ConfigureCore(config, employeeId, isPpmAdministrator);

        return config;
    }

    private static void ConfigureCore(TypeAdapterConfig config, Guid? employeeId, bool isPpmAdministrator)
    {
        config.NewConfig<ProjectPortfolio, ProjectPortfolioDetailsDto>()
            .Map(dest => dest.Status, src => LifecycleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.ScoringModel, src => src.ScoringModel == null
                ? null
                : NavigationDto.Create(src.ScoringModel.Id, src.ScoringModel.Key, src.ScoringModel.Name))
            .Map(dest => dest.PortfolioSponsors, src => src.Roles.Where(r => r.Role == ProjectPortfolioRole.Sponsor).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.PortfolioOwners, src => src.Roles.Where(r => r.Role == ProjectPortfolioRole.Owner).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.PortfolioManagers, src => src.Roles.Where(r => r.Role == ProjectPortfolioRole.Manager).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            // Mirrors ProjectPortfolio.CanManagePortfolio: a portfolio has no ancestor, so only its own
            // Owner/Manager roles qualify — or the administrator grant.
            .Map(dest => dest.CanManagePortfolio, src => isPpmAdministrator || (employeeId.HasValue &&
                src.Roles.Any(r => r.EmployeeId == employeeId.Value &&
                    (r.Role == ProjectPortfolioRole.Owner || r.Role == ProjectPortfolioRole.Manager))));
    }
}
