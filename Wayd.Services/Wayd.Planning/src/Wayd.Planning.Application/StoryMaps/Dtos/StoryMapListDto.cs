using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Extensions;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Dtos;

/// <summary>
/// A Story Map summarised for lists: name, description, owners, access and status, without its content.
/// </summary>
public sealed record StoryMapListDto : IMapFrom<StoryMap>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }

    /// <summary>
    /// Public: anyone with the Story Maps View permission can see the map. Private: only its owners and editors.
    /// </summary>
    public Visibility Visibility { get; set; }

    /// <summary>
    /// Who can change a public map's content: anyone who can view it, or only its owners and editors.
    /// </summary>
    public StoryMapEditAccess EditAccess { get; set; }

    /// <summary>
    /// The employees who own the map.
    /// </summary>
    public List<EmployeeNavigationDto> Owners { get; set; } = [];

    /// <summary>
    /// Whether the caller manages the map, as an owner or a Story Maps administrator. Always false for a read-only
    /// token or an account with no linked employee.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Whether the caller can change the map's content. Says nothing about whether the map is archived. Always false
    /// for a read-only token or an account with no linked employee.
    /// </summary>
    public bool CanEdit { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<StoryMap, StoryMapListDto>()
            .Map(dest => dest.Status, src => src.Status.GetDisplayName())
            .Map(dest => dest.Owners, src => src.Owners.OrderBy(o => o.Employee!.Name.FirstName).ThenBy(o => o.Employee!.Name.LastName).Select(o => EmployeeNavigationDto.From(o.Employee!)))
            .Ignore(dest => dest.CanManage)
            .Ignore(dest => dest.CanEdit);
    }
}
