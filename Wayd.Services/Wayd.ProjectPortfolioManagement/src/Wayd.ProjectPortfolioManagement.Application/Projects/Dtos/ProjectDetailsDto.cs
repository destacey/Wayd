using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

public sealed record ProjectDetailsDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// The unique key of the project.  This is an alternate key to the Id.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The name of the project.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// A concise summary of what the project delivers and its scope.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// The strategic justification for the project — why it should be funded.
    /// Captures the problem being solved or opportunity being pursued and the strategic rationale.
    /// </summary>
    public string? BusinessCase { get; set; }

    /// <summary>
    /// The specific, measurable outcomes expected upon successful delivery.
    /// Examples: revenue growth, cost savings, compliance achievement, efficiency improvements.
    /// </summary>
    public string? ExpectedBenefits { get; set; }

    /// <summary>
    /// The current status of the project.
    /// </summary>
    public required LifecycleNavigationDto Status { get; set; }

    /// <summary>
    /// The expenditure category associated with the project.
    /// </summary>
    public required SimpleNavigationDto ExpenditureCategory { get; set; }

    /// <summary>
    /// The project start date.
    /// </summary>
    public LocalDate? Start { get; set; }

    /// <summary>
    /// The project end date.
    /// </summary>
    public LocalDate? End { get; set; }

    /// <summary>
    /// The portfolio associated with this project.
    /// </summary>
    public required NavigationDto Portfolio { get; set; }

    /// <summary>
    /// The program associated with this project.
    /// </summary>
    public NavigationDto? Program { get; set; }

    /// <summary>
    /// The sponsors of the project.
    /// </summary>
    public required List<EmployeeNavigationDto> ProjectSponsors { get; set; } = [];

    /// <summary>
    /// The owners of the project.
    /// </summary>
    public required List<EmployeeNavigationDto> ProjectOwners { get; set; } = [];

    /// <summary>
    /// The managers of the project.
    /// </summary>
    public required List<EmployeeNavigationDto> ProjectManagers { get; set; } = [];

    /// <summary>
    /// The members of the project team.
    /// </summary>
    public required List<EmployeeNavigationDto> ProjectMembers { get; set; } = [];

    /// <summary>
    /// The strategic themes associated with this project.
    /// </summary>
    public required List<NavigationDto> StrategicThemes { get; set; } = [];

    /// <summary>
    /// The strategic initiatives this project is supporting.
    /// </summary>
    public required List<NavigationDto> StrategicInitiatives { get; set; } = [];

    /// <summary>
    /// The project lifecycle assigned to this project.
    /// </summary>
    public DescriptiveNavigationDto? ProjectLifecycle { get; set; }

    /// <summary>
    /// The project phases, ordered by display order.
    /// </summary>
    public List<ProjectPhaseListDto> Phases { get; set; } = [];

    /// <summary>
    /// The current (non-expired) health check for this project, or null if none exists.
    /// </summary>
    public ProjectHealthCheckDto? HealthCheck { get; set; }

    /// <summary>
    /// Whether the current user can manage this project (record health checks, scores, and other
    /// delivery-management actions). True when the user is an Owner or Manager of the project, its
    /// portfolio, or its program. May be elevated by a second-pass portfolio/program check in the
    /// query handler.
    /// </summary>
    public bool CanManageProject { get; set; }

    /// <summary>
    /// The scoring model assigned to the project's portfolio, or null if scoring is not enabled.
    /// </summary>
    public NavigationDto? PortfolioScoringModel { get; set; }

    /// <summary>
    /// A summary of the project's most recent score, or null if it has not been scored.
    /// </summary>
    public ScoreSummaryDto? CurrentScore { get; set; }

    /// <summary>
    /// The project's fractional rank sort key within its portfolio — the same value as the domain
    /// <c>Project.Rank</c> (opaque; not the displayed position). Every project is ranked from creation.
    /// Used to order the ranking board and drive client-side reorder logic; not for display.
    /// </summary>
    public double Rank { get; set; }

    /// <summary>
    /// Creates a TypeAdapterConfig that maps <see cref="Project"/> to <see cref="ProjectDetailsDto"/>,
    /// including the current health check and project-level authorization.
    /// <para>
    /// This is the <b>only</b> supported mapping path for this DTO. There is no global Mapster
    /// registration — calling <c>project.Adapt&lt;ProjectDetailsDto&gt;()</c> or
    /// <c>ProjectToType&lt;ProjectDetailsDto&gt;()</c> without this config will produce incorrect
    /// results (null health check, false CanManageHealthChecks).
    /// </para>
    /// Pass <paramref name="employeeId"/> as null when no user is authenticated.
    /// </summary>
    public static TypeAdapterConfig CreateTypeAdapterConfig(Instant now, Guid? employeeId)
    {
        var cfg = new TypeAdapterConfig();
        ProjectPhaseListDto.RegisterMapping(cfg);

        cfg.NewConfig<Project, ProjectDetailsDto>()
            .Map(dest => dest.Key, src => src.Key.Value)
            .Map(dest => dest.Status, src => LifecycleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.ExpenditureCategory, src => SimpleNavigationDto.Create(src.ExpenditureCategory!.Id, src.ExpenditureCategory.Name))
            .Map(dest => dest.Start, src => src.DateRange != null ? src.DateRange.Start : (LocalDate?)null)
            .Map(dest => dest.End, src => src.DateRange != null ? src.DateRange.End : (LocalDate?)null)
            .Map(dest => dest.Portfolio, src => NavigationDto.Create(src.Portfolio!.Id, src.Portfolio.Key, src.Portfolio.Name))
            .Map(dest => dest.Program, src => src.Program != null ? NavigationDto.Create(src.Program.Id, src.Program.Key, src.Program.Name) : null)
            .Map(dest => dest.ProjectSponsors, src => src.Roles.Where(r => r.Role == ProjectRole.Sponsor).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.ProjectOwners, src => src.Roles.Where(r => r.Role == ProjectRole.Owner).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.ProjectManagers, src => src.Roles.Where(r => r.Role == ProjectRole.Manager).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.ProjectMembers, src => src.Roles.Where(r => r.Role == ProjectRole.Member).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.StrategicThemes, src => src.StrategicThemeTags.Select(x => NavigationDto.Create(x.StrategicTheme!.Id, x.StrategicTheme.Key, x.StrategicTheme.Name)).ToList())
            .Map(dest => dest.StrategicInitiatives, src => src.StrategicInitiativeProjects.Select(x => NavigationDto.Create(x.StrategicInitiative!.Id, x.StrategicInitiative.Key, x.StrategicInitiative.Name)).ToList())
            .Map(dest => dest.ProjectLifecycle, src => src.ProjectLifecycle != null ? DescriptiveNavigationDto.Create(src.ProjectLifecycle.Id, src.ProjectLifecycle.Key, src.ProjectLifecycle.Name, src.ProjectLifecycle.Description) : null)
            .Map(dest => dest.Phases, src => src.Phases.OrderBy(p => p.Order))
            .Map(dest => dest.HealthCheck, src => src.HealthChecks
                .Where(h => !h.IsDeleted && h.Expiration > now)
                .Select(h => new ProjectHealthCheckDto
                {
                    Id = h.Id,
                    Status = SimpleNavigationDto.FromEnum(h.Status),
                    ReportedBy = NavigationDto.Create(h.ReportedBy.Id, h.ReportedBy.Key, h.ReportedBy.Name.DisplayName),
                    ReportedOn = h.ReportedOn,
                    Expiration = h.Expiration,
                    Note = h.Note
                })
                .FirstOrDefault())
            .Map(dest => dest.CanManageProject, src => employeeId.HasValue &&
                src.Roles.Any(r => r.EmployeeId == employeeId.Value &&
                    (r.Role == ProjectRole.Owner || r.Role == ProjectRole.Manager)))
            .Map(dest => dest.PortfolioScoringModel, src => src.Portfolio!.ScoringModel == null
                ? null
                : NavigationDto.Create(src.Portfolio.ScoringModel.Id, src.Portfolio.ScoringModel.Key, src.Portfolio.ScoringModel.Name))
            .Map(dest => dest.CurrentScore, src => src.CurrentScore == null ? null : new ScoreSummaryDto
            {
                Value = src.CurrentScore.Value,
                ScoredOn = src.CurrentScore.ScoredOn,
                ScoringModelId = src.CurrentScore.ScoringModelId,
                ScoredBy = src.CurrentScore.ScoredBy == null
                    ? null
                    : EmployeeNavigationDto.From(src.CurrentScore.ScoredBy),
                ScoringModelName = src.CurrentScore.ScoringModelName,
            });

        return cfg;
    }
}
