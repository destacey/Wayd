using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// A named target a release is deployed into, ordered into rings so progressive rollout is
/// representable.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Named <c>DeploymentEnvironment</c>, not <c>Environment</c>.</strong> Implicit usings put
/// <c>System.Environment</c> in scope in every file, so a domain type called <c>Environment</c> would
/// shadow it and force fully-qualified references in unrelated code. The extra word is cheaper than
/// that ambiguity.
/// </para>
/// <para>
/// <strong>Scoping is global.</strong> The design leaves this open — per product, inherited from an
/// ancestor, or global with opt-in — and this is the simplest thing that works: environments are
/// defined once for the organization and any product can deploy into any of them. Ring order is
/// therefore a global concept, which is what makes "deployments to production, last 90 days" a single
/// query with no tree walk. Per-product scoping can be added later as an optional owner column without
/// invalidating anything recorded, since a global environment is just one with no owner. Going the
/// other way — starting per-product and generalising — would need every existing row re-homed.
/// </para>
/// </remarks>
public sealed class DeploymentEnvironment : BaseAuditableEntity, IHasIdAndKey
{
    private DeploymentEnvironment() { }

    private DeploymentEnvironment(string name, EnvironmentCategory category, int ringOrder)
    {
        Name = name;
        Category = category;
        RingOrder = ringOrder;
    }

    /// <summary>
    /// The unique auto-generated key of the environment. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// What the organization calls it — "Production", "prod-eu", "QA2".
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// What kind of target this is. Every measure scoped to production counts on this rather than on
    /// the name, which is free text and endlessly varied.
    /// </summary>
    public EnvironmentCategory Category { get; private set; }

    /// <summary>
    /// Position in a progressive rollout, lowest first. Environments sharing a ring are deployed to
    /// together; the value carries no meaning beyond ordering.
    /// </summary>
    public int RingOrder { get; private set; }

    /// <summary>
    /// Whether this environment can still be deployed into.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Renames the environment and repositions it in the rollout order.
    /// </summary>
    public Result Update(string name, int ringOrder)
    {
        if (!IsActive)
        {
            return Result.Failure("A retired environment cannot be updated.");
        }

        Name = name;
        RingOrder = ringOrder;

        return Result.Success();
    }

    /// <summary>
    /// Changes what kind of target this environment is.
    /// </summary>
    /// <remarks>
    /// Looks like configuration and is not: marking an environment as production retroactively changes
    /// deployment frequency and every measure scoped to production, so a number reported last week can
    /// move without any deployment having happened. Hence its own event rather than passing as part of
    /// <see cref="Update"/>.
    /// </remarks>
    public Result Reclassify(EnvironmentCategory category, EventActor actor, Instant timestamp)
    {
        if (!IsActive)
        {
            return Result.Failure("A retired environment cannot be reclassified.");
        }

        if (category == Category)
        {
            return Result.Success();
        }

        var fromCategory = Category;
        Category = category;

        AddDomainEvent(new EnvironmentReclassifiedEvent(Id, Key, Name, fromCategory, category, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Takes the environment out of use so nothing further can be deployed into it.
    /// </summary>
    /// <remarks>
    /// Deactivated rather than deleted: historical deployments still point here, and "what was running
    /// in production on this date" has to keep resolving after an environment is decommissioned.
    /// </remarks>
    public Result Deactivate(EventActor actor, Instant timestamp)
    {
        if (!IsActive)
        {
            return Result.Failure("This environment is already inactive.");
        }

        IsActive = false;

        AddDomainEvent(new EnvironmentRetiredEvent(Id, Key, Name, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Puts the environment back into use.
    /// </summary>
    /// <remarks>
    /// A decommissioned environment sometimes comes back — a region re-enabled, a test bed rebuilt —
    /// and its historical deployments should stay attached to the same environment rather than a
    /// duplicate.
    /// </remarks>
    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Failure("This environment is already active.");
        }

        IsActive = true;

        return Result.Success();
    }

    /// <summary>
    /// Defines an environment.
    /// </summary>
    public static DeploymentEnvironment Create(string name, EnvironmentCategory category, int ringOrder, EventActor actor, Instant timestamp)
    {
        var environment = new DeploymentEnvironment(name, category, ringOrder);

        // Deferred because Key is database-generated: an event raised here would carry Key 0.
        environment.AddPostPersistenceAction(() => environment.AddDomainEvent(
            new EnvironmentAddedEvent(environment.Id, environment.Key, environment.Name, environment.Category, environment.RingOrder, actor, timestamp)));

        return environment;
    }
}
