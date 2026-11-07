using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Integrations.AzureDevOps.Models.Contracts;

namespace Wayd.Integrations.AzureDevOps.Models.Processes;

internal sealed record ProcessWorkItemTypeDto
{
    public required string ReferenceName { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Inherits { get; set; }
    public bool IsDisabled { get; set; }
    public List<ProcessWorkItemStateDto> States { get; set; } = [];
    public List<ProcessWorkItemTypeBehaviorsDto> Behaviors { get; set; } = [];
}

internal static class ProcessWorkItemTypeDtoExtensions
{
    // TODO make this configurable
    static readonly string[] _ignoredWorkItemTypes =
        [
            "Microsoft.VSTS.WorkItemTypes.Task",
            "Microsoft.VSTS.WorkItemTypes.Issue",
            "Microsoft.VSTS.WorkItemTypes.TestCase",
            "Microsoft.VSTS.WorkItemTypes.TestPlan",
            "Microsoft.VSTS.WorkItemTypes.TestSuite"
        ];

    public static AzdoWorkType ToAzdoWorkType(this ProcessWorkItemTypeDto workItemType)
    {
        // TODO make this configurable
        var backlogLevelId = workItemType.Behaviors.FirstOrDefault()?.Behavior.Id ?? "System.RequirementBacklogBehavior";

        var azdoWorkType = new AzdoWorkType
        {
            ExternalKey = workItemType.ReferenceName,
            Name = workItemType.Name,
            Description = workItemType.Description,
            WorkTypeLevelId = backlogLevelId,
            IsActive = !workItemType.IsDisabled,
            WorkflowStates = []
            //WorkflowStates = workItemType.States.Select(workItemState => workItemState.ToAzdoWorkflowState()).ToList<IExternalWorkflowState>()
        };

        // there seems to be a bug in the API when including states that duplicates hidden states. Override with the last one for now.
        foreach (var group in workItemType.States.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var state = group.Last().ToAzdoWorkflowState();
            state.StatusName = group.Key;
            state.ExternalKey = KeyOf(group).Id.ToString("D");
            azdoWorkType.WorkflowStates.Add(state);
        }

        return azdoWorkType;
    }

    /// <summary>
    /// The copy of a state whose id keys it. A visible copy is preferred, since it is the one that stays
    /// should the API stop returning the hidden duplicate; the lowest id breaks a tie, so the key does not
    /// depend on the order the copies arrive in.
    /// </summary>
    private static ProcessWorkItemStateDto KeyOf(IEnumerable<ProcessWorkItemStateDto> copies) =>
        copies.OrderBy(c => c.Hidden).ThenBy(c => c.Id.ToString("D"), StringComparer.Ordinal).First();

    public static IList<IExternalWorkTypeWorkflow> ToIExternalWorkTypes(this List<ProcessWorkItemTypeDto> workItemTypes)
    {
        // test work types typically have no behaviors
        return workItemTypes
            .Where(w => !_ignoredWorkItemTypes.Contains(w.ReferenceName)
                && (w.Inherits is null || !_ignoredWorkItemTypes.Contains(w.Inherits)))
            .Select(w => w.ToAzdoWorkType())
            .ToList<IExternalWorkTypeWorkflow>();
    }

    public static IList<IExternalWorkStatus> ToIExternalWorkStatuses(this List<ProcessWorkItemTypeDto> workItemTypes)
    {
        return workItemTypes
            .Where(w => !_ignoredWorkItemTypes.Contains(w.ReferenceName)
                && (w.Inherits is null || !_ignoredWorkItemTypes.Contains(w.Inherits)))
            .SelectMany(w => w.States)
            .DistinctBy(s => s.Name)
            .Select(s => s.ToAzdoWorkStatus())
            .ToList<IExternalWorkStatus>();
    }
}
