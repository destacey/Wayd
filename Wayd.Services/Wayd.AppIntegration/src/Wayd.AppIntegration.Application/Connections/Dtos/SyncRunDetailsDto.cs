using Mapster;
using NodaTime;
using Wayd.Common.Application.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Dtos;

public sealed record SyncRunDetailsDto : IMapFrom<SyncRun>
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public Connector ConnectorType { get; set; }
    public Instant StartedAt { get; set; }
    public Instant? FinishedAt { get; set; }
    public SyncRunStatus Status { get; set; }
    public SyncTriggerSource TriggerSource { get; set; }
    public SyncType SyncType { get; set; }
    public int WorkspacesPlanned { get; set; }
    public int WorkspacesSucceeded { get; set; }
    public int WorkspacesFailed { get; set; }
    public int WorkItemsProcessed { get; set; }
    public int ErrorsCount { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Connector-specific per-run details as a JSON string. The shape varies by connector
    /// category: work-sync runs carry an array of workspace details, people-sync runs carry a
    /// single employees summary object.
    /// </summary>
    /// <remarks>Parsed by the matching frontend detail view.</remarks>
    public string? DetailsJson { get; set; }
}
