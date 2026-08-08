using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;

public sealed record ProgramDetailsDto : IMapFrom<Program>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The unique key of the program.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the program.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// A detailed description of the programs's purpose and scope.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// The current status of the program.
    /// </summary>
    public required LifecycleNavigationDto Status { get; set; }

    /// <summary>
    /// The program start date.
    /// </summary>
    public LocalDate? Start { get; set; }

    /// <summary>
    /// The program end date.
    /// </summary>
    public LocalDate? End { get; set; }

    /// <summary>
    /// The portfolio associated with this program.
    /// </summary>
    public required NavigationDto Portfolio { get; set; }

    /// <summary>
    /// The sponsors of the program.
    /// </summary>
    public required List<EmployeeNavigationDto> ProgramSponsors { get; set; } = [];

    /// <summary>
    /// The owners of the program.
    /// </summary>
    public required List<EmployeeNavigationDto> ProgramOwners { get; set; } = [];

    /// <summary>
    /// The managers of the program.
    /// </summary>
    public required List<EmployeeNavigationDto> ProgramManagers { get; set; } = [];

    /// <summary>
    /// The strategic themes associated with this program.
    /// </summary>
    public required List<NavigationDto> StrategicThemes { get; set; } = [];

    /// <summary>
    /// Whether the current user may manage this program — change its status, edit its details, or assign
    /// its roles. True when the user is an Owner or Manager of the program or of its parent portfolio, or
    /// holds the domain-wide PPM administrator grant. Sponsors are excluded.
    /// <para>
    /// This is a UI hint only; the aggregate enforces the same rule. It is false when the DTO is produced
    /// by the parameterless global mapping, so callers that need it must use
    /// <see cref="CreateTypeAdapterConfig"/>.
    /// </para>
    /// </summary>
    public bool CanManageProgram { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        ConfigureCore(config, employeeId: null, isPpmAdministrator: false);
    }

    /// <summary>
    /// Creates a config that maps <see cref="Program"/> to this DTO including the
    /// <see cref="CanManageProgram"/> authorization hint for the given actor.
    /// </summary>
    /// <param name="employeeId">The current user's linked employee, or null when unauthenticated.</param>
    /// <param name="isPpmAdministrator">
    /// Whether the current user holds the domain-wide PPM administrator grant, which substitutes for role
    /// membership. Must mirror <see cref="Program.CanManageProgram"/>.
    /// </param>
    public static TypeAdapterConfig CreateTypeAdapterConfig(Guid? employeeId, bool isPpmAdministrator)
    {
        var config = new TypeAdapterConfig();
        ConfigureCore(config, employeeId, isPpmAdministrator);

        return config;
    }

    private static void ConfigureCore(TypeAdapterConfig config, Guid? employeeId, bool isPpmAdministrator)
    {
        config.NewConfig<Program, ProgramDetailsDto>()
            .Map(dest => dest.Status, src => LifecycleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.Start, src => src.DateRange != null ? src.DateRange.Start : (LocalDate?)null)
            .Map(dest => dest.End, src => src.DateRange != null ? src.DateRange.End : (LocalDate?)null)
            .Map(dest => dest.Portfolio, src => NavigationDto.Create(src.Portfolio!.Id, src.Portfolio.Key, src.Portfolio.Name))
            .Map(dest => dest.ProgramSponsors, src => src.Roles.Where(r => r.Role == ProgramRole.Sponsor).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.ProgramOwners, src => src.Roles.Where(r => r.Role == ProgramRole.Owner).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.ProgramManagers, src => src.Roles.Where(r => r.Role == ProgramRole.Manager).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.StrategicThemes, src => src.StrategicThemeTags.Select(x => NavigationDto.Create(x.StrategicTheme!.Id, x.StrategicTheme.Key, x.StrategicTheme.Name)).ToList())
            // Mirrors Program.CanManageProgram: Owner/Manager on the program or on the parent portfolio,
            // or the administrator grant. Evaluated inline as SQL subqueries.
            .Map(dest => dest.CanManageProgram, src => isPpmAdministrator || (employeeId.HasValue && (
                src.Roles.Any(r => r.EmployeeId == employeeId.Value &&
                    (r.Role == ProgramRole.Owner || r.Role == ProgramRole.Manager)) ||
                src.Portfolio!.Roles.Any(r => r.EmployeeId == employeeId.Value &&
                    (r.Role == ProjectPortfolioRole.Owner || r.Role == ProjectPortfolioRole.Manager)))));
    }
}
