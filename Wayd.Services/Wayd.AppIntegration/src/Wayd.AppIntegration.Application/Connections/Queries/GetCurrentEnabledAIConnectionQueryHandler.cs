using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Requests.AppIntegration;

namespace Wayd.AppIntegration.Application.Connections.Queries;

public sealed class GetAIConnectionQueryHandler(IAppIntegrationDbContext appIntegrationDbContext) : IQueryHandler<GetAIConnectionQuery, EnabledAIConnectionDto>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;

    public async Task<EnabledAIConnectionDto> Handle(GetAIConnectionQuery request, CancellationToken cancellationToken)
    {
        // var query = _appIntegrationDbContext.Connections.AsQueryable();

        // return await query.ProjectToType<EnabledAIConnectionDto>().FirstOrDefaultAsync(cancellationToken);
        throw new NotImplementedException();
    }
}
