using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.DemandProjects.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Reads the outcomes of intake requests, in the shape a request's page and the list beside it show them.
/// </summary>
/// <remarks>
/// Outcomes are summary tier, so the requests given need only be narrowed to those the reader can see. A work item
/// outcome's <see cref="IntakeRequestOutcomeDto.WorkItem"/> is left null: Demand Management keeps no copy of work
/// items, so the API layer fills it in from Work.
/// </remarks>
internal static class IntakeRequestOutcomeReader
{
    /// <summary>
    /// The outcomes of each of the requests that has any, by request number, each request's oldest first and each
    /// project outcome with its project when the copy still has it. A request with no outcome is absent.
    /// </summary>
    public static async Task<List<(Guid RequestId, IReadOnlyList<IntakeRequestOutcomeDto> Outcomes)>> Read(
        IDemandManagementDbContext dbContext,
        IQueryable<IntakeRequest> requests,
        CancellationToken cancellationToken)
    {
        var employees = dbContext.Employees;

        var outcomes = await requests
            .SelectMany(r => r.Outcomes, (r, o) => new
            {
                RequestId = r.Id,
                RequestKey = r.Key,
                o.Kind,
                o.TargetId,
                o.Note,
                AddedBy = employees.Where(e => e.Id == o.AddedById)
                    .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                    .First(),
                o.AddedOn,
                DeliverableIds = o.Deliverables.Select(d => d.DeliverableId).ToList(),
            })
            .OrderBy(o => o.RequestKey)
            .ThenBy(o => o.AddedOn)
            .ThenBy(o => o.TargetId)
            .ToListAsync(cancellationToken);
        if (outcomes.Count == 0)
            return [];

        var projectIds = outcomes.Where(o => o.Kind != IntakeRequestOutcomeKind.WorkItem).Select(o => o.TargetId).Distinct().ToList();
        var projects = projectIds.Count == 0
            ? []
            : await dbContext.DemandProjects
                .AsNoTracking()
                .Where(p => projectIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

        var mappedIds = outcomes.SelectMany(o => o.DeliverableIds).Distinct().ToList();
        var deliverables = mappedIds.Count == 0
            ? []
            : await dbContext.DemandProjectDeliverables
                .AsNoTracking()
                .Where(d => mappedIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, cancellationToken);

        return
        [
            .. outcomes
                .GroupBy(o => o.RequestId)
                .Select(g => (
                    g.Key,
                    (IReadOnlyList<IntakeRequestOutcomeDto>)
                    [
                        .. g.Select(o => new IntakeRequestOutcomeDto
                        {
                            Kind = o.Kind,
                            TargetId = o.TargetId,
                            Project = projects.TryGetValue(o.TargetId, out var p)
                                ? new DemandProjectNavigationDto { Id = p.Id, Key = p.Key.Value, Name = p.Name }
                                : null,
                            Note = o.Note,
                            Deliverables =
                            [
                                .. o.DeliverableIds
                                    .Select(id => IntakeRequestOutcomeDeliverableDto.From(id, deliverables.GetValueOrDefault(id)))
                                    .OrderBy(d => d.IsOnProject ? 0 : 1)
                                    .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(d => d.Id),
                            ],
                            IsMet = IntakeRequestOutcome.IsMet(o.DeliverableIds.Select(deliverables.GetValueOrDefault).OfType<DemandProjectDeliverable>()),
                            AddedBy = o.AddedBy,
                            AddedOn = o.AddedOn,
                        }),
                    ])),
        ];
    }

    /// <summary>
    /// Whether a request is met by the deliverables its outcomes map: every outcome that maps one is met, or null
    /// when none maps one.
    /// </summary>
    public static bool? DeliverablesMet(IReadOnlyList<IntakeRequestOutcomeDto> outcomes)
    {
        var mapped = outcomes.Where(o => o.IsMet is not null).ToList();
        return mapped.Count == 0 ? null : mapped.All(o => o.IsMet == true);
    }
}
