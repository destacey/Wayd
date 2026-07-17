using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Extensions;
using Wayd.Work.Domain.Interfaces;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// A workflow is a sequence of work statuses that a work item can move through.  It is used to define the process for a work item.
/// </summary>
/// <seealso cref="Wayd.Common.Domain.Data.BaseSoftDeletableEntity&lt;System.Guid&gt;" />
/// <seealso cref="Wayd.Common.Domain.Interfaces.IActivatable" />
public sealed class Workflow : BaseSoftDeletableEntity, IActivatable, IHasIdAndKey
{
    private readonly List<WorkflowScheme> _schemes = [];

    private Workflow() { }

    internal Workflow(string name, string? description, Ownership ownership)
    {
        Name = name;
        Description = description;
        Ownership = ownership;
    }

    /// <summary>
    /// The key of the workflow.  This is the unique identifier for the workflow and should not change.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the workflow.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>
    /// The description of the workflow.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// Indicates whether the workflow is owned by Wayd or a third party system.  This value should not change.
    /// </summary>
    public Ownership Ownership { get; private init; }

    /// <summary>
    /// Indicates whether the workflow is active or not.  Only active workflows can be assigned 
    /// to work process configurations.  The default is false and the user should activate it after the configuration is complete.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<WorkflowScheme> Schemes => _schemes.AsReadOnly();

    /// <summary>
    /// The process for activating a workflow.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(Instant timestamp)
    {
        if (!IsActive)
        {
            // TODO is there logic that would prevent activation?
            IsActive = true;
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating a workflow.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Deactivate(Instant timestamp)
    {
        if (IsActive)
        {
            // TODO is there logic that would prevent deactivation?
            IsActive = false;

            foreach (var scheme in _schemes)
            {
                var deactivateResult = scheme.Deactivate(timestamp);
                if (deactivateResult.IsFailure)
                {
                    return Result.Failure(deactivateResult.Error);
                }
            }
        }

        return Result.Success();
    }

    public Result Update(string name, string? description, Instant timestamp)
    {
        Name = name;
        Description = description;

        return Result.Success();
    }

    public Result AddScheme(int workStatusId, WorkStatusCategory workStatusCategory, int order, bool isActive)
    {
        if (_schemes.Any(s => s.WorkStatusId == workStatusId))
        {
            return Result.Failure($"Scheme for Work Status {workStatusId} already exists.");
        }
        _schemes.Add(WorkflowScheme.Create(this, workStatusId, workStatusCategory, order, isActive));
        return Result.Success();
    }

    public Result ActivateScheme(Guid schemeId, Instant timestamp)
    {
        var scheme = _schemes.SingleOrDefault(s => s.Id == schemeId);
        if (scheme is null)
        {
            return Result.Failure($"Scheme {schemeId} not found.");
        }

        if (!IsActive)
        {
            return Result.Failure("Unable to active a Workflow Scheme while the Workflow is not active.");
        }

        return scheme.Activate(timestamp);
    }

    public Result DeactivateScheme(Guid schemeId, Instant timestamp)
    {
        var scheme = _schemes.SingleOrDefault(s => s.Id == schemeId);
        if (scheme is null)
        {
            return Result.Failure($"Scheme {schemeId} not found.");
        }

        return scheme.Deactivate(timestamp);
    }


    public static Workflow CreateExternal(string name, string? description, IEnumerable<ICreateWorkflowScheme> workflowSchemes)
    {
        var workflow = new Workflow(name, description, Ownership.Managed);

        foreach (var scheme in workflowSchemes)
        {
            var result = workflow.AddScheme(scheme.WorkStatusId, scheme.Category, scheme.Order, scheme.IsActive);
            if (result.IsFailure)
            {
                throw new InvalidOperationException(result.Error);
            }
        }

        return workflow;
    }
}
