using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;

public sealed record StrategicInitiativeDetailsDto : IMapFrom<StrategicInitiative>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The strategic initiative's unique key, an alternate to its <c>id</c>. Endpoints differ in which of the two they accept; a uuid parameter takes the id.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the strategic initiative.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// A detailed explanation of what the strategic initiative aims to achieve.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// The status of the strategic initiative.
    /// </summary>
    public required LifecycleNavigationDto Status { get; set; }

    /// <summary>
    /// The strategic initiative start date.
    /// </summary>
    public LocalDate? Start { get; set; }

    /// <summary>
    /// The strategic initiative end date.
    /// </summary>
    public LocalDate? End { get; set; }

    /// <summary>
    /// The portfolio associated with this strategic initiative.
    /// </summary>
    public required NavigationDto Portfolio { get; set; }

    /// <summary>
    /// The sponsors of the strategic initiative.
    /// </summary>
    public required List<EmployeeNavigationDto> StrategicInitiativeSponsors { get; set; } = [];

    /// <summary>
    /// The owners of the strategic initiative.
    /// </summary>
    public required List<EmployeeNavigationDto> StrategicInitiativeOwners { get; set; } = [];

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<StrategicInitiative, StrategicInitiativeDetailsDto>()
            .Map(dest => dest.Status, src => LifecycleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.Start, src => src.DateRange != null ? src.DateRange.Start : (LocalDate?)null)
            .Map(dest => dest.End, src => src.DateRange != null ? src.DateRange.End : (LocalDate?)null)
            .Map(dest => dest.Portfolio, src => NavigationDto.Create(src.Portfolio!.Id, src.Portfolio.Key, src.Portfolio.Name))
            .Map(dest => dest.StrategicInitiativeSponsors, src => src.Roles.Where(r => r.Role == StrategicInitiativeRole.Sponsor).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.StrategicInitiativeOwners, src => src.Roles.Where(r => r.Role == StrategicInitiativeRole.Owner).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList());
    }
}
