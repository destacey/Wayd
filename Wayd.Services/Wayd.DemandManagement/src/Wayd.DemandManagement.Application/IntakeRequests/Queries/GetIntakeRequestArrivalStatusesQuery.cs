using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The statuses an intake request can arrive in when moved to a desk, and the one it arrives in unless the
/// mover chooses; or null when the reader cannot see the request or there is no such desk.
/// </summary>
/// <remarks>
/// Offered to whoever can move the request — an agent on its current desk — by the rules the move command
/// applies. Anyone else gets an empty list.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="DeskId">The desk it would move to.</param>
public sealed record GetIntakeRequestArrivalStatusesQuery(Guid Id, Guid DeskId) : IQuery<IntakeRequestArrivalStatusesDto?>;

public sealed class GetIntakeRequestArrivalStatusesQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IStatusResolver statusResolver)
    : IQueryHandler<GetIntakeRequestArrivalStatusesQuery, IntakeRequestArrivalStatusesDto?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IStatusResolver _statusResolver = statusResolver;

    public async Task<IntakeRequestArrivalStatusesDto?> Handle(GetIntakeRequestArrivalStatusesQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var intakeRequest = await _dbContext.IntakeRequests
            .AsNoTracking()
            .Where(r => r.Id == request.Id)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .FirstOrDefaultAsync(cancellationToken);
        if (intakeRequest is null || !await _dbContext.IntakeDesks.AnyAsync(d => d.Id == request.DeskId, cancellationToken))
            return null;

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return new IntakeRequestArrivalStatusesDto { Statuses = [] };

        var workflow = await _statusResolver.ForDesk(request.DeskId, cancellationToken);
        if (workflow.IsFailure)
            return new IntakeRequestArrivalStatusesDto { Statuses = [] };

        var (statuses, fallback) = IntakeRequestStatuses.ArrivalStatuses(workflow.Value, intakeRequest);

        return new IntakeRequestArrivalStatusesDto
        {
            Statuses = [.. statuses.Select(s => new StatusNavigationDto { Id = s.Id, Name = s.Name, Category = s.Category, Alias = s.Alias })],
            DefaultStatusId = fallback?.Id,
        };
    }
}
