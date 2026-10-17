using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;

public sealed record ProgramListDto : IMapFrom<Program>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The program's unique key, an alternate to its <c>id</c>. Endpoints differ in which of the two they accept; a uuid parameter takes the id.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the program.
    /// </summary>
    public required string Name { get; set; }

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

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Program, ProgramListDto>()
            .Map(dest => dest.Status, src => LifecycleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.Start, src => src.DateRange != null ? src.DateRange.Start : (LocalDate?)null)
            .Map(dest => dest.End, src => src.DateRange != null ? src.DateRange.End : (LocalDate?)null)
            .Map(dest => dest.Portfolio, src => NavigationDto.Create(src.Portfolio!.Id, src.Portfolio.Key, src.Portfolio.Name))
            .Map(dest => dest.ProgramSponsors, src => src.Roles.Where(r => r.Role == ProgramRole.Sponsor).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.ProgramOwners, src => src.Roles.Where(r => r.Role == ProgramRole.Owner).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.ProgramManagers, src => src.Roles.Where(r => r.Role == ProgramRole.Manager).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.StrategicThemes, src => src.StrategicThemeTags.Select(x => NavigationDto.Create(x.StrategicTheme!.Id, x.StrategicTheme.Key, x.StrategicTheme.Name)).ToList());
    }
}
