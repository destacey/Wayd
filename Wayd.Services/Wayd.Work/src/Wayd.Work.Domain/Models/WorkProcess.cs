using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Extensions;
using NodaTime;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// The work process defines a set of work process configurations that can be used
/// within a workspace. A work process can be used in many workspaces.
/// </summary>
/// <seealso cref="Wayd.Common.Domain.Data.BaseSoftDeletableEntity" />
/// <seealso cref="Wayd.Common.Domain.Interfaces.IActivatable" />
public sealed class WorkProcess : BaseSoftDeletableEntity, IActivatable<WorkProcessActivationArgs>, IHasIdAndKey, IHasOwnershipInfo
{
    private readonly List<Workspace> _workspaces = [];

    private WorkProcess() { }

    private WorkProcess(string name, string? description, OwnershipInfo ownershipInfo)
    {
        Name = name;
        Description = description;
        OwnershipInfo = Guard.Against.Null(ownershipInfo);
    }

    /// <summary>Gets the key.</summary>
    /// <value>The key.</value>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the work process.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>
    /// The description of the work process.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// Who writes the work process, and the process in an outside system it corresponds to. For Azure DevOps the
    /// external id is the process id, which is the same in every organization, so no system id is held.
    /// </summary>
    public OwnershipInfo OwnershipInfo { get; private init; } = default!;

    /// <summary>
    /// The outside system's process id, or null when the work process does not correspond to one. Read from
    /// <see cref="OwnershipInfo"/> in memory; a query filters on <c>OwnershipInfo.ExternalId</c> with
    /// <see cref="ToExternalId"/> instead.
    /// </summary>
    public Guid? ExternalProcessId => Guid.TryParse(OwnershipInfo.ExternalId, out var id) ? id : null;

    /// <summary>
    /// An outside system's process id as it is stored in <c>OwnershipInfo.ExternalId</c>.
    /// </summary>
    public static string ToExternalId(Guid externalProcessId) => externalProcessId.ToString("D");

    /// <summary>
    /// Indicates whether the work process is active or not.  Only active work processes can be assigned
    /// to workspaces.  The default is false.
    /// </summary>
    /// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
    public bool IsActive { get; private set; } = false;

    public IReadOnlyCollection<Workspace> Workspaces => _workspaces.AsReadOnly();

    public Result Update(string name, string? description, Instant timestamp)
    {
        Name = name;
        Description = description;

        return Result.Success();
    }

    /// <summary>
    /// The process for activating a work process.  A work process can only be activated if the configuration
    /// is valid.
    /// </summary>
    /// <param name="args">The actor making the change and the timestamp of it.</param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(WorkProcessActivationArgs args)
    {
        if (!IsActive)
        {
            // TODO is there logic that would prevent activation?
            IsActive = true;

            TryAddIntegrationStateChangedEvent(args.Actor, args.Timestamp);
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating a work process.  Only work processes without active assignments can be deactivated.
    /// </summary>
    /// <param name="args">The actor making the change and the timestamp of it.</param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Deactivate(WorkProcessActivationArgs args)
    {
        // TODO what is need to deactive a managed work process

        if (IsActive)
        {
            if (Workspaces.Any(w => w.IsActive))
                return Result.Failure("Unable to deactive with active workspaces.");

            IsActive = false;

            TryAddIntegrationStateChangedEvent(args.Actor, args.Timestamp);
        }

        return Result.Success();
    }

    private void TryAddIntegrationStateChangedEvent(EventActor actor, Instant timestamp)
    {
        if (OwnershipInfo.Ownership is Ownership.Managed)
        {
            AddDomainEvent(new IntegrationStateChangedEvent<Guid>(
                SystemContext.WorkWorkProcess,
                IntegrationState<Guid>.Create(Id, IsActive),
                actor,
                timestamp));
        }
    }

    /// <summary>Creates a work process a sync manages, for a process in an outside system.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="connector">The kind of outside system the process is in.</param>
    /// <param name="externalProcessId">The outside system's process id.</param>
    /// <param name="timestamp">The timestamp.</param>
    public static WorkProcess CreateExternal(string name, string? description, Connector connector, Guid externalProcessId, Instant timestamp)
    {
        var ownershipInfo = OwnershipInfo.CreateExternalOwned(connector, null, ToExternalId(externalProcessId));
        return new WorkProcess(name, description, ownershipInfo);
    }
}
