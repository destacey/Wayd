using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// An operational incident, linked to one or more products and tracked through a simple open lifecycle.
/// </summary>
public sealed class Incident : StatusTrackedEntity, IHasIdAndKey
{
    private readonly List<IncidentProductLink> _productLinks = [];

    private Incident() { }

    private Incident(
        string title,
        string? description,
        Guid commanderEmployeeId,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt)
    {
        Title = title;
        Description = description;
        CommanderEmployeeId = commanderEmployeeId;
        SeverityLevelId = severityLevelId;
        NoticedAt = noticedAt;
        StartedAt = startedAt;
        RecoveredAt = recoveredAt;
    }

    /// <inheritdoc/>
    public override string StatusOwnerType => ProductWorkflowOwners.Incident.Key;

    /// <summary>The unique auto-generated key of the incident.</summary>
    public int Key { get; private init; }

    /// <summary>What happened, in a line a responder can scan.</summary>
    public string Title
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Title)).Trim();
    } = default!;

    /// <summary>Additional context for responders.</summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>The employee commanding the response.</summary>
    public Guid CommanderEmployeeId { get; private init; }

    /// <summary>The severity level from the organization's selected scale.</summary>
    public Guid SeverityLevelId { get; private set; }

    /// <summary>When the incident was first noticed.</summary>
    public Instant NoticedAt { get; private set; }

    /// <summary>When active response began, if recorded.</summary>
    public Instant? StartedAt { get; private set; }

    /// <summary>When service was restored, if recorded.</summary>
    public Instant? RecoveredAt { get; private set; }

    /// <summary>The products implicated in the incident.</summary>
    public IReadOnlyCollection<IncidentProductLink> ProductLinks => _productLinks.AsReadOnly();

    /// <summary>The well-known meaning of the current status.</summary>
    public ProductStatusAlias StatusAlias => (ProductStatusAlias)StatusAliasValue;

    /// <summary>
    /// Opens a new incident in the <see cref="ProductStatusAlias.Open"/> status.
    /// </summary>
    public static Result<Incident> Create(
        string title,
        string? description,
        Guid commanderEmployeeId,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt,
        IReadOnlyCollection<Guid> productIds,
        StatusRef openStatus,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(openStatus, nameof(openStatus));

        if (commanderEmployeeId == Guid.Empty)
        {
            return Result.Failure<Incident>("A commander is required.");
        }

        if (severityLevelId == Guid.Empty)
        {
            return Result.Failure<Incident>("A severity level is required.");
        }

        var timeline = ValidateTimeline(noticedAt, startedAt, recoveredAt);
        if (timeline.IsFailure)
        {
            return Result.Failure<Incident>(timeline.Error);
        }

        var incident = new Incident(
            title,
            description,
            commanderEmployeeId,
            severityLevelId,
            noticedAt,
            startedAt,
            recoveredAt);

        var applied = incident.ApplyStatus(openStatus, actor, timestamp);
        if (applied.IsFailure)
        {
            return Result.Failure<Incident>(applied.Error);
        }

        var products = ReplaceProductLinks(incident, productIds);
        if (products.IsFailure)
        {
            return Result.Failure<Incident>(products.Error);
        }

        return Result.Success(incident);
    }

    /// <summary>
    /// Updates the editable details of an open incident.
    /// </summary>
    public Result UpdateDetails(
        string title,
        string? description,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt,
        IReadOnlyCollection<Guid> productIds)
    {
        if (severityLevelId == Guid.Empty)
        {
            return Result.Failure("A severity level is required.");
        }

        var timeline = ValidateTimeline(noticedAt, startedAt, recoveredAt);
        if (timeline.IsFailure)
        {
            return timeline;
        }

        Title = title;
        Description = description;
        SeverityLevelId = severityLevelId;
        NoticedAt = noticedAt;
        StartedAt = startedAt;
        RecoveredAt = recoveredAt;

        return ReplaceProductLinks(this, productIds);
    }

    private static Result ValidateTimeline(Instant noticedAt, Instant? startedAt, Instant? recoveredAt)
    {
        if (startedAt is not null && startedAt.Value < noticedAt)
        {
            return Result.Failure("Response cannot start before the incident was noticed.");
        }

        if (recoveredAt is not null && startedAt is not null && recoveredAt.Value < startedAt.Value)
        {
            return Result.Failure("Recovery cannot be before response started.");
        }

        if (recoveredAt is not null && startedAt is null && recoveredAt.Value < noticedAt)
        {
            return Result.Failure("Recovery cannot be before the incident was noticed.");
        }

        return Result.Success();
    }

    private static Result ReplaceProductLinks(Incident incident, IReadOnlyCollection<Guid> productIds)
    {
        var distinct = productIds.Where(id => id != Guid.Empty).Distinct().ToList();
        incident._productLinks.Clear();

        foreach (var productId in distinct)
        {
            incident._productLinks.Add(new IncidentProductLink(incident.Id, productId));
        }

        return Result.Success();
    }
}
