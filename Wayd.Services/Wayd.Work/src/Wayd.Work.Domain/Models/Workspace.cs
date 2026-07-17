using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Extensions;
using Wayd.Common.Models;
using Wayd.Work.Domain.Interfaces;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// Represents a workspace that organizes and manages work items, processes, and related metadata.
/// </summary>
/// <remarks>A workspace is a central entity that contains work items and is associated with a specific work
/// process. It supports operations such as adding and removing work items, updating workspace properties, and managing
/// activation and deactivation states. Workspaces can be either owned internally or managed externally, with optional
/// support for external work item URL templates.</remarks>
public sealed class Workspace : BaseSoftDeletableEntity, IActivatable<WorkspaceActivatableArgs, Instant>, IHasWorkspaceIdAndKey
{
    private readonly List<WorkItem> _workItems = [];

    private Workspace() { }

    private Workspace(WorkspaceKey key, string name, string? description, OwnershipInfo ownershipInfo, Guid workProcessId, string? externalViewWorkItemUrlTemplate)
    {
        Key = key;
        Name = name;
        Description = description;
        OwnershipInfo = Guard.Against.Null(ownershipInfo);
        WorkProcessId = workProcessId;

        if (OwnershipInfo.Ownership is Ownership.Managed)
        {
            ExternalViewWorkItemUrlTemplate = externalViewWorkItemUrlTemplate;
        }
    }

    /// <summary>A unique key to identify the workspace.</summary>
    /// <value>The key.</value>
    public WorkspaceKey Key
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(Key));
    } = null!;

    /// <summary>
    /// The name of the workspace.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>
    /// The description of the workspace.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The ownership information for this workspace.
    /// </summary>
    public OwnershipInfo OwnershipInfo { get; private set; } = default!;

    /// <summary>
    /// The foreign key for the work process.
    /// </summary>
    public Guid WorkProcessId { get; private set; }

    /// <summary>
    /// The work process assigned to the workspace.
    /// </summary>
    public WorkProcess? WorkProcess { get; private set; }

    /// <summary>
    /// A url template for external work items.  This template plus the work item external id will create a url to view the work item in the external system.
    /// </summary>
    public string? ExternalViewWorkItemUrlTemplate
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// Indicates whether the workspace is active or not.  Inactive workspaces will be locked.  This means that
    /// users won't be able to add or updated work items.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// A collection of work items in the workspace.
    /// </summary>
    public IReadOnlyCollection<WorkItem> WorkItems => _workItems.AsReadOnly();

    /// <summary>
    /// Creates a work item within the workspace.
    /// </summary>
    /// <param name="workItem"></param>
    /// <returns></returns>
    public Result AddWorkItem(WorkItem workItem)
    {
        if (!IsActive)
            return Result.Failure("Unable to create work items within an inactive work space.");

        // TODO: validate unique key, currently handled by the database

        _workItems.Add(workItem);
        return Result.Success();
    }

    /// <summary>
    /// Removes work items from the workspace.
    /// </summary>
    /// <param name="workItemIds"></param>
    /// <returns></returns>
    public Result RemoveWorkItems(IEnumerable<Guid> workItemIds)
    {
        if (!IsActive)
            return Result.Failure("Unable to remove work items in an inactive work space.");

        foreach (var workItemId in workItemIds)
        {
            var workItem = _workItems.FirstOrDefault(x => x.Id == workItemId);
            if (workItem != null)
                _workItems.Remove(workItem);
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for updating the workspace properties.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result Update(string name, string? description, Instant timestamp)
    {
        var newName = name.Trim();
        var newDescription = description?.Trim();

        // return success if no changes
        if (string.Equals(Name, newName, StringComparison.Ordinal)
            && string.Equals(Description, newDescription, StringComparison.Ordinal))
            return Result.Success();

        Name = newName;
        Description = newDescription;

        return Result.Success();
    }

    public Result SetSystemId(string systemId)
    {
        // TODO: this method is temporary until all external workspaces have their system ids set

        if (OwnershipInfo.Ownership is not Ownership.Managed)
            return Result.Failure($"Unable to set the SystemId for an {OwnershipInfo.Ownership.GetDisplayName()} workspace.");

        var systemIdTrimmed = systemId?.Trim();

        if (OwnershipInfo.SystemId is not null)
        {
            return OwnershipInfo.SystemId == systemIdTrimmed
                ? Result.Success()
                : Result.Failure("SystemId has already been set and cannot be changed to a different value.");
        }

        OwnershipInfo = OwnershipInfo.CreateExternalOwned((Connector)OwnershipInfo.Connector!, systemIdTrimmed, OwnershipInfo.ExternalId!);

        return Result.Success();
    }

    /// <summary>
    /// Set's the work item's external URL template
    /// </summary>
    /// <param name="externalWorkItemUrlTemplate"></param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public Result SetExternalViewWorkItemUrlTemplate(string? externalWorkItemUrlTemplate, Instant timestamp)
    {
        if (OwnershipInfo.Ownership is not Ownership.Managed)
            return Result.Failure($"Unable to set the external view work item url template for an {OwnershipInfo.Ownership.GetDisplayName()} workspace.");

        var newExternalWorkItemUrlTemplate = externalWorkItemUrlTemplate?.Trim();

        // return success if no changes
        if (string.Equals(ExternalViewWorkItemUrlTemplate, newExternalWorkItemUrlTemplate, StringComparison.Ordinal))
            return Result.Success();

        // TODO: validate the template

        ExternalViewWorkItemUrlTemplate = newExternalWorkItemUrlTemplate;

        return Result.Success();
    }

    /// <summary>
    /// Changes the work process assigned to this externally-managed workspace.
    /// This is used when the external system (e.g., Azure DevOps) reassigns
    /// a project to a different process.
    /// </summary>
    /// <param name="newWorkProcessId">The internal ID of the new work process.</param>
    /// <param name="timestamp">The timestamp of the change.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result ChangeWorkProcess(Guid newWorkProcessId, Instant timestamp)
    {
        Guard.Against.Default(newWorkProcessId, nameof(newWorkProcessId));

        if (OwnershipInfo.Ownership is not Ownership.Managed)
            return Result.Failure("Unable to change the work process on a non-managed workspace.");

        if (WorkProcessId == newWorkProcessId)
            return Result.Success();

        WorkProcessId = newWorkProcessId;

        return Result.Success();
    }

    /// <summary>The process for activating a workspace.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(WorkspaceActivatableArgs args)
    {
        if (WorkProcessId != args.WorkProcess.Id)
            return Result.Failure($"Unable to activate the workspace because the work process does not match the workspace work process.");

        if (!args.WorkProcess.IsActive)
            return Result.Failure($"Unable to activate the workspace because the work process is not active.");

        if (!IsActive)
        {
            IsActive = true;
        }

        return Result.Success();
    }

    /// <summary>The process for deactivating a workspace.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Deactivate(Instant timestamp)
    {
        if (IsActive)
        {
            // TODO is there logic that would prevent deactivation?
            IsActive = false;
        }

        return Result.Success();
    }

    /// <summary>Creates an owned Workspace.</summary>
    /// <param name="key">The key.</param>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="workProcessId">The work process.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public static Workspace Create(WorkspaceKey key, string name, string? description, Guid workProcessId, Instant timestamp)
    {
        var workspace = new Workspace(key, name, description, OwnershipInfo.CreateWaydOwned(), workProcessId, null);

        return workspace;
    }

    /// <summary>Creates a managed Workspace linked to a external id.</summary>
    /// <param name="key">The key.</param>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="externalId">The external identifier.</param>
    /// <param name="workProcess">The work process.</param>
    /// <param name="externalViewWorkItemUrlTemplate">The external view work item URL template.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public static Workspace CreateExternal(WorkspaceKey key, string name, string? description, OwnershipInfo ownershipInfo, Guid workProcessId, string? externalViewWorkItemUrlTemplate, Instant timestamp)
    {
        var workspace = new Workspace(key, name, description, ownershipInfo, workProcessId, externalViewWorkItemUrlTemplate);

        return workspace;
    }
}
