using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The deliverables of a project that an agent may map an existing project scope outcome of a request to, by
/// name; null when the reader may not change the request's outcomes or the project has no copy.
/// </summary>
/// <remarks>
/// Read from Demand Management's copy rather than PPM, so an agent needs no PPM permission to choose. Gated by
/// the request rather than the project: only an agent on the request's desk may change what it maps, so only
/// they are shown the choice. Descoped deliverables are listed too, since one may already be mapped.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="ProjectId">The project.</param>
public sealed record GetIntakeRequestProjectDeliverablesQuery(Guid Id, Guid ProjectId)
    : IQuery<IReadOnlyList<IntakeRequestProjectDeliverableDto>?>;

/// <summary>A deliverable of a project, as offered for mapping to an intake request's outcome.</summary>
public sealed record IntakeRequestProjectDeliverableDto
{
    /// <summary>The deliverable, by its id in PPM.</summary>
    public Guid Id { get; init; }

    /// <summary>What the deliverable is called.</summary>
    public required string Name { get; init; }

    /// <summary>The category of the deliverable's status.</summary>
    public StatusCategory StatusCategory { get; init; }

    /// <summary>The well-known meaning of the deliverable's status.</summary>
    public ProjectDeliverableStatusAlias StatusAlias { get; init; }
}

/// <summary>Lists a project's deliverable copies for an agent on the request's desk.</summary>
public sealed class GetIntakeRequestProjectDeliverablesQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeRequestProjectDeliverablesQuery, IReadOnlyList<IntakeRequestProjectDeliverableDto>?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<IReadOnlyList<IntakeRequestProjectDeliverableDto>?> Handle(GetIntakeRequestProjectDeliverablesQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var deskId = await _dbContext.IntakeRequests
            .Where(r => r.Id == request.Id)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .Select(r => (Guid?)r.DeskId)
            .FirstOrDefaultAsync(cancellationToken);
        if (deskId is null || !reader.IsAgentOn(deskId.Value))
            return null;

        if (!await _dbContext.DemandProjects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken))
            return null;

        return await _dbContext.DemandProjectDeliverables
            .Where(d => d.ProjectId == request.ProjectId)
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .Select(d => new IntakeRequestProjectDeliverableDto
            {
                Id = d.Id,
                Name = d.Name,
                StatusCategory = d.StatusCategory,
                StatusAlias = d.StatusAlias,
            })
            .ToListAsync(cancellationToken);
    }
}
