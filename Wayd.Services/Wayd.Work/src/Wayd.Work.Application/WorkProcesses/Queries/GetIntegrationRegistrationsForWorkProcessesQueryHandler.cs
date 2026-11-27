using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Queries;

public sealed class GetIntegrationRegistrationsForWorkProcessesQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetIntegrationRegistrationsForWorkProcessesQuery, List<IntegrationRegistration<Guid, Guid>>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<List<IntegrationRegistration<Guid, Guid>>> Handle(GetIntegrationRegistrationsForWorkProcessesQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkProcesses
            .Where(w => w.OwnershipInfo.Ownership == Ownership.Managed && w.OwnershipInfo.ExternalId != null);

        if (request.ExternalId is { } requested)
        {
            var externalId = WorkProcess.ToExternalId(requested);
            query = query.Where(w => w.OwnershipInfo.ExternalId == externalId);
        }

        var processes = await query
            .Select(w => new { w.OwnershipInfo.ExternalId, w.Id, w.IsActive })
            .ToListAsync(cancellationToken);

        // The external id is held as text. A registration is keyed by a process id, so a work process whose
        // external id is not one has no registration to report.
        List<IntegrationRegistration<Guid, Guid>> registrations = [];
        foreach (var process in processes)
        {
            if (Guid.TryParse(process.ExternalId, out var externalProcessId))
                registrations.Add(new IntegrationRegistration<Guid, Guid>(externalProcessId, IntegrationState<Guid>.Create(process.Id, process.IsActive)));
        }

        return registrations;
    }
}
