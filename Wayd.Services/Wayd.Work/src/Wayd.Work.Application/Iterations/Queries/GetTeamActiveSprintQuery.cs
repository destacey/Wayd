using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed record GetTeamActiveSprintQuery(Guid TeamId) : IQuery<SprintDetailsDto?>;

public sealed class GetTeamActiveSprintQueryValidator : CustomValidator<GetTeamActiveSprintQuery>
{
    public GetTeamActiveSprintQueryValidator()
    {
        RuleFor(q => q.TeamId)
            .NotEmpty();
    }
}

public sealed class GetTeamActiveSprintQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetTeamActiveSprintQuery, SprintDetailsDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<SprintDetailsDto?> Handle(GetTeamActiveSprintQuery request, CancellationToken cancellationToken)
    {
        var sprint = await _workDbContext.Iterations
            .Where(i => i.TeamId == request.TeamId && i.Type == IterationType.Sprint && i.State == IterationState.Active)
            .ProjectToType<SprintDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return sprint;
    }
}
