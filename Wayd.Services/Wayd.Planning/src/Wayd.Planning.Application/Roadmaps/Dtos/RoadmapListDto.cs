using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Dtos;

public sealed record RoadmapListDto : IMapFrom<Roadmap>
{
    /// <summary>The roadmap's unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The roadmap's numeric key, an alternate to its id.</summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the Roadmap.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The Roadmap start date.
    /// </summary>
    public required LocalDate Start { get; set; }

    /// <summary>
    /// The Roadmap end date.
    /// </summary>
    public required LocalDate End { get; set; }

    /// <summary>
    /// The visibility of the Roadmap. If the Roadmap is public, all users can see the Roadmap. Otherwise, only its owners and Roadmaps administrators can see it.
    /// </summary>
    public required SimpleNavigationDto Visibility { get; set; }

    /// <summary>
    /// The state of the Roadmap.
    /// </summary>
    public required SimpleNavigationDto State { get; set; }

    /// <summary>
    /// The color of the Roadmap. Must be a valid hex color code.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// The owners of the Roadmap.
    /// </summary>
    public required List<EmployeeNavigationDto> Owners { get; set; } = [];

    /// <summary>
    /// Whether the caller can manage the Roadmap and its items: they own it or hold the Roadmaps Administer
    /// permission. False when the request's token is read-only. An archived Roadmap can only be activated.
    /// </summary>
    public bool CanManage { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Roadmap, RoadmapListDto>()
            .Map(dest => dest.Start, src => src.DateRange.Start)
            .Map(dest => dest.End, src => src.DateRange.End)
            .Map(dest => dest.Visibility, src => SimpleNavigationDto.FromEnum(src.Visibility))
            .Map(dest => dest.State, src => SimpleNavigationDto.FromEnum(src.State))
            .Map(dest => dest.Owners, src => src.Owners.Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Ignore(dest => dest.CanManage);
    }
}
