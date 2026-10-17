using Wayd.Common.Application.Requests.WorkManagement.Interfaces;

namespace Wayd.Work.Application.WorkStatuses.Dtos;

public sealed record WorkStatusDto : IMapFrom<WorkStatus>, IWorkStatusDto
{
    public int Id { get; set; }

    /// <summary>The name of the work status. It cannot be changed.</summary>
    public required string Name { get; set; }

    /// <summary>The description of the work status; null when it has none.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the work status is active.</summary>
    public bool IsActive { get; set; }
}
