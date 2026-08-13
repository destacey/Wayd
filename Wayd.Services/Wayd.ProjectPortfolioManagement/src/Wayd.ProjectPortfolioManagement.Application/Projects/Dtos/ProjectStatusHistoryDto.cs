using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Identity;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

public sealed record ProjectStatusHistoryDto : IMapFrom<ProjectStatusHistory>
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>
    /// The status the project moved out of, or null when the project entered its initial state.
    /// </summary>
    public LifecycleNavigationDto? FromStatus { get; set; }

    /// <summary>
    /// The status the project moved into.
    /// </summary>
    public required LifecycleNavigationDto ToStatus { get; set; }

    /// <summary>
    /// The employee who made the change, or null when it was made by the system or by a user with no
    /// employee link at the time.
    /// </summary>
    public NavigationDto? ChangedBy { get; set; }

    /// <summary>
    /// Whether the change was made by the system rather than a person. Resolved from the recorded user
    /// account, not from the absence of <see cref="ChangedBy"/> — a signed-in user with no employee link
    /// also has no employee on the row, and must not be reported as the system.
    /// </summary>
    public bool ChangedBySystem { get; set; }

    public Instant ChangedOn { get; set; }

    /// <summary>
    /// Whether the row was recorded as the transition happened or reconstructed from the audit trail.
    /// </summary>
    public required SimpleNavigationDto Source { get; set; }

    public string? Reason { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectStatusHistory, ProjectStatusHistoryDto>()
            .Map(dest => dest.FromStatus, src => src.FromStatus.HasValue
                ? LifecycleNavigationDto.FromEnum(src.FromStatus.Value)
                : null)
            .Map(dest => dest.ToStatus, src => LifecycleNavigationDto.FromEnum(src.ToStatus))
            .Map(dest => dest.Source, src => SimpleNavigationDto.FromEnum(src.Source))
            .Map(dest => dest.ChangedBy, src => src.ChangedByEmployee != null
                ? NavigationDto.Create(src.ChangedByEmployee.Id, src.ChangedByEmployee.Key, src.ChangedByEmployee.Name.DisplayName)
                : null)
            .Map(dest => dest.ChangedBySystem, src => src.ChangedByUserId == SystemUser.Id);
    }
}
