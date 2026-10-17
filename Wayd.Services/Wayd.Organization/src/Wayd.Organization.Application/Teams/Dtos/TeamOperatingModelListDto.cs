using System.ComponentModel.DataAnnotations;
using Mapster;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Organization.Application.Teams.Dtos;

/// <summary>
/// A team's operating model in brief: the methodology and sizing method it uses.
/// </summary>
public sealed record TeamOperatingModelListDto : IMapFrom<TeamOperatingModel>
{
    /// <summary>
    /// The identifier of the operating model.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// The methodology the team uses (e.g., Scrum, Kanban).
    /// </summary>
    [Required]
    public Methodology Methodology { get; set; }

    /// <summary>
    /// The sizing method the team uses (e.g., StoryPoints, Count).
    /// </summary>
    [Required]
    public SizingMethod SizingMethod { get; set; }
}
