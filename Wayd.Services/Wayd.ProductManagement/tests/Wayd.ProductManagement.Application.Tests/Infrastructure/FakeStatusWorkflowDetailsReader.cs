using System.Linq.Expressions;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Application.Tests.Infrastructure;

/// <summary>
/// A details reader holding one workflow, so a handler is shown to ask for its record's own workflow:
/// a filter matching any other answers <c>null</c>.
/// </summary>
public sealed class FakeStatusWorkflowDetailsReader(StatusWorkflow workflow) : IStatusWorkflowDetailsReader
{
    private readonly StatusWorkflow _workflow = workflow;

    /// <summary>What a read matching the held workflow returns.</summary>
    public StatusWorkflowDetailsDto Details { get; } = new()
    {
        Id = workflow.Id,
        Key = 1,
        Name = workflow.Name,
        Owner = new WorkflowOwnerNavigationDto { Key = workflow.OwnerType, Name = workflow.OwnerType },
        State = workflow.State.ToString(),
        IsSystem = false,
        IsAssigned = true,
        Statuses = [],
        TransitionMode = TransitionMode.AnyToAny,
        Transitions = [],
        AvailableConditions = [],
        DeclaredMoves = [],
        SupportsRestrictedTransitions = true,
        TransitionIssues = [],
        MissingRequiredAliases = [],
        CanEdit = false,
        CanPublish = false,
        CanArchive = false,
    };

    public int ReadCount { get; private set; }

    /// <summary>What the last read was told about which conditions something can check.</summary>
    public Func<TransitionConditionKind, bool>? IsConditionAvailable { get; private set; }

    public Task<StatusWorkflowDetailsDto?> Read(
        Expression<Func<StatusWorkflow, bool>> filter,
        Func<TransitionConditionKind, bool> isConditionAvailable,
        CancellationToken cancellationToken)
    {
        ReadCount++;
        IsConditionAvailable = isConditionAvailable;

        return Task.FromResult(filter.Compile()(_workflow) ? Details : null);
    }
}
