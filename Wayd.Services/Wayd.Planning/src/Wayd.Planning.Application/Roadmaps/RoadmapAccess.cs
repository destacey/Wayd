using System.Linq.Expressions;
using Wayd.Common.Domain.Authorization;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps;

/// <summary>
/// Resolves the caller into what a <see cref="Roadmap"/> needs to decide what they may do: their employee and whether
/// they hold the Roadmaps Administer permission. The roadmap itself makes the decision.
/// </summary>
public static class RoadmapAccess
{
    /// <summary>Lets its holder see and manage every roadmap, private ones included.</summary>
    public static readonly string AdministratorPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.Roadmaps);

    /// <summary>
    /// The caller as a <see cref="RoadmapActor"/>. Commands using this must be marked <c>IRequireLinkedEmployee</c>.
    /// Call it outside a handler's catch-all, so the refusal reaches the caller as a 403.
    /// </summary>
    /// <exception cref="ForbiddenException">Thrown via <c>LinkedEmployeeRequired</c> when the caller has no linked employee.</exception>
    public static async Task<RoadmapActor> ResolveRoadmapActor(this ICurrentPrincipal currentPrincipal, CancellationToken cancellationToken)
    {
        var employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            LinkedEmployeeRequired.Throw();

        var isAdministrator = await currentPrincipal.HasPermission(AdministratorPermission, cancellationToken);

        return new RoadmapActor(employeeId.Value, isAdministrator);
    }

    /// <summary>
    /// The caller as a reader of roadmaps: which they can see and manage.
    /// </summary>
    public static async Task<RoadmapReader> ResolveRoadmapReader(this ICurrentPrincipal currentPrincipal, CancellationToken cancellationToken)
    {
        var employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        var isAdministrator = await currentPrincipal.HoldsPermission(AdministratorPermission, cancellationToken);

        return new RoadmapReader(employeeId, isAdministrator, currentPrincipal.IsReadOnly);
    }
}

/// <summary>
/// The caller as a reader of roadmaps.
/// </summary>
/// <param name="EmployeeId">The reader's employee, or null when the account has none.</param>
/// <param name="IsAdministrator">Whether the reader holds the Roadmaps Administer permission.</param>
/// <param name="IsReadOnly">The request's token can change nothing, so the reader manages nothing.</param>
public sealed record RoadmapReader(Guid? EmployeeId, bool IsAdministrator, bool IsReadOnly)
{
    /// <summary>The roadmaps the reader can see, as <see cref="Roadmap.AccessibleTo"/> decides.</summary>
    public Expression<Func<Roadmap, bool>> Filter => Roadmap.AccessibleTo(EmployeeId, IsAdministrator);

    /// <summary>
    /// The reader as an actor, to ask which roadmaps they manage; null when they can change nothing, having no
    /// employee (every roadmap command needs one) or a read-only token.
    /// </summary>
    public RoadmapActor? Actor => !IsReadOnly && EmployeeId is { } id
        ? new RoadmapActor(id, IsAdministrator)
        : null;

    /// <summary>
    /// The ids of the roadmaps in <paramref name="roadmaps"/> the reader manages, as <see cref="Roadmap.CanManage"/>
    /// decides; none when <see cref="Actor"/> is null.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> Managed(IQueryable<Roadmap> roadmaps, CancellationToken cancellationToken)
    {
        if (Actor is not { } actor)
            return new HashSet<Guid>();

        return await roadmaps
            .Where(Roadmap.ManagedBy(actor))
            .Select(r => r.Id)
            .ToHashSetAsync(cancellationToken);
    }
}
