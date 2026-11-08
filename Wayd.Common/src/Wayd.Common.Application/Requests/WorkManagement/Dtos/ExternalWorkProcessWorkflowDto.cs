namespace Wayd.Common.Application.Requests.WorkManagement.Dtos;

/// <summary>The Work workflow one work type follows in a work process, as the sync reads it.</summary>
/// <param name="WorkTypeName">The work type's name, which the sync matches the source's types on.</param>
/// <param name="WorkflowId">The workflow's id, or null when the type has none yet.</param>
/// <param name="WorkflowName">The workflow's name, or null when the type has none yet.</param>
/// <param name="WorkflowDescription">The workflow's description, or null when it has none.</param>
public sealed record ExternalWorkProcessWorkflowDto(string WorkTypeName, Guid? WorkflowId, string? WorkflowName, string? WorkflowDescription);
