using System.Linq.Expressions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Reads one workflow with its statuses, transitions and declared moves, as its detail page shows it.
/// </summary>
/// <remarks>
/// A service rather than only a query so a module's handler can resolve a record's workflow and read it
/// in one pass, under that module's permission: someone who can see a product can see the workflow it
/// moves through without holding the settings permission the workflow pages need.
/// </remarks>
public interface IStatusWorkflowDetailsReader : IScopedService
{
    /// <summary>The first workflow matching <paramref name="filter"/>, or <c>null</c> when none does.</summary>
    /// <param name="isConditionAvailable">
    /// Whether something can check a condition, from <c>ITransitionConditionChecker</c>. Passed in by the
    /// handler because one service may not depend on another.
    /// </param>
    Task<StatusWorkflowDetailsDto?> Read(
        Expression<Func<StatusWorkflow, bool>> filter,
        Func<TransitionConditionKind, bool> isConditionAvailable,
        CancellationToken cancellationToken);
}
