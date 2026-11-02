using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A workflow scheme was created, empty, as a workflow's implicit default-only scheme, or as a copy of
/// another.
/// </summary>
public sealed record WorkflowSchemeCreatedEvent : DomainEvent<WorkflowSchemeCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public WorkflowSchemeCreatedEvent(
        Guid id,
        int key,
        string ownerType,
        string name,
        string? description,
        WorkflowOwnership ownership,
        string? externalKey,
        bool isImplicit,
        Guid defaultWorkflowId,
        WorkflowSchemeEntryValues[] entries,
        Guid? sourceSchemeId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OwnerType = ownerType;
        Name = name;
        Description = description;
        Ownership = ownership;
        ExternalKey = externalKey;
        IsImplicit = isImplicit;
        DefaultWorkflowId = defaultWorkflowId;
        Entries = [.. entries];
        SourceSchemeId = sourceSchemeId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The owner type whose records it sends to workflows, fixed for the scheme's life.</summary>
    public string OwnerType { get; }

    public string Name { get; }
    public string? Description { get; }

    /// <summary>Whether administrators keep it or a sync does.</summary>
    public WorkflowOwnership Ownership { get; }

    /// <summary>The source's identifier for a managed scheme; null for an owned one.</summary>
    public string? ExternalKey { get; }

    /// <summary>Whether it is a workflow's default-only scheme, which is never shown as a scheme.</summary>
    public bool IsImplicit { get; }

    /// <summary>The workflow records follow when their type has no entry.</summary>
    public Guid DefaultWorkflowId { get; }

    /// <summary>The entries it was created with, by record type id.</summary>
    public WorkflowSchemeEntryValues[] Entries { get; }

    /// <summary>The scheme this one was cloned from, or null when it was not a clone.</summary>
    public Guid? SourceSchemeId { get; }

    [JsonIgnore]
    public string AggregateType => "WorkflowScheme";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
