using CSharpFunctionalExtensions;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Builds the remaps a command needs from two loaded workflows and the operator's decisions.
/// </summary>
public static class StatusRemaps
{
    /// <summary>
    /// The automatic mapping from <paramref name="from"/> to <paramref name="to"/>, with every decision that
    /// moves a status of <paramref name="from"/> into one of <paramref name="to"/> layered on top.
    /// </summary>
    /// <remarks>
    /// Decisions for other pairs of workflows are ignored, so one list can serve a change that moves several
    /// record types between different workflows: status ids are never shared between workflows, so the two
    /// ends of a decision name its pair. Both workflows need their statuses loaded.
    /// </remarks>
    /// <returns>A failure when a decision cannot be applied, or the mapping is left incomplete.</returns>
    public static Result<StatusRemap> Build(StatusWorkflow from, StatusWorkflow to, IReadOnlyCollection<StatusRemapDecisionDto> decisions)
    {
        var remap = StatusRemap.AutoMap(from, to);
        if (remap.IsFailure)
        {
            return remap;
        }

        foreach (var decision in decisions)
        {
            if (from.Statuses.All(s => s.Id != decision.FromStatusId)
                || to.Statuses.FirstOrDefault(s => s.Id == decision.ToStatusId) is not { } target)
            {
                continue;
            }

            var resolved = remap.Value.Resolve(decision.FromStatusId, target);
            if (resolved.IsFailure)
            {
                return Result.Failure<StatusRemap>(resolved.Error);
            }
        }

        if (!remap.Value.IsComplete)
        {
            var unresolved = string.Join(", ", remap.Value.Unresolved.Select(s => s.Name));

            return Result.Failure<StatusRemap>(
                $"These statuses of '{from.Name}' have nowhere to go in '{to.Name}': {unresolved}. Map every status first.");
        }

        return remap;
    }

    /// <summary>
    /// A complete mapping for a change that moves no records, so there is nothing for an operator to decide:
    /// whatever the automatic mapping cannot place goes to the target's initial status, only so the domain's
    /// completeness check is met.
    /// </summary>
    public static Result<StatusRemap> ForNoRecords(StatusWorkflow from, StatusWorkflow to)
    {
        var remap = StatusRemap.AutoMap(from, to);
        if (remap.IsFailure || to.InitialStatus is not { } initial)
        {
            return remap;
        }

        foreach (var unresolved in remap.Value.Unresolved.ToList())
        {
            remap.Value.Resolve(unresolved.Id, initial);
        }

        return remap;
    }
}
