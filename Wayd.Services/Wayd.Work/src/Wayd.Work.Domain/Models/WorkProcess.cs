using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Extensions;
using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// The work process defines a set of work process configurations that can be used
/// within a workspace. A work process can be used in many workspaces.
/// </summary>
/// <seealso cref="Wayd.Common.Domain.Data.BaseSoftDeletableEntity" />
/// <seealso cref="Wayd.Common.Domain.Interfaces.IActivatable" />
public sealed class WorkProcess : BaseSoftDeletableEntity, IActivatable<WorkProcessActivationArgs>, IHasIdAndKey
{
    private readonly List<Workspace> _workspaces = [];

    private WorkProcess() { }

    private WorkProcess(string name, string? description, Ownership ownership, Guid? externalId)
    {
        Name = name;
        Description = description;
        Ownership = ownership;

        if (ownership is Ownership.Managed)
        {
            if (!externalId.HasValue)
                throw new ArgumentException("The external identifier is required when the ownership is managed.", nameof(externalId));

            ExternalId = externalId.Value;
        }
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
    /// Indicates whether the work process is owned by Wayd or a third party system.  This value should not change.
    /// </summary>
    /// <value>The ownership.</value>
    public Ownership Ownership { get; private init; }

    /// <summary>
    /// Gets the external identifier. The value is required when Ownership is managed; otherwise it's null.  For Azure DevOps, this is the process id.
    /// </summary>
    public Guid? ExternalId { get; private init; }

    /// <summary>
    /// Indicates whether the work process is active or not.  Only active work processes can be assigned
    /// to workspaces.  The default is false.
    /// </summary>
    /// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
    public bool IsActive { get; private set; } = false;

    public IReadOnlyCollection<Workspace> Workspaces => _workspaces.AsReadOnly();

    public Result Update(string name, string? description, Instant timestamp)
    {
        try
        {
            Name = name;
            Description = description;

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
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
        if (Ownership is Ownership.Managed && ExternalId.HasValue)
        {
            AddDomainEvent(new IntegrationStateChangedEvent<Guid>(
                SystemContext.WorkWorkProcess,
                IntegrationState<Guid>.Create(Id, IsActive),
                actor,
                timestamp));
        }
    }

    /// <summary>Creates a managed Work Process linked to a external id.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="externalId">The external identifier.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public static WorkProcess CreateExternal(string name, string? description, Guid externalId, Instant timestamp)
    {
        WorkProcess workProcess = new(name, description, Ownership.Managed, externalId);
        return workProcess;
    }
}
