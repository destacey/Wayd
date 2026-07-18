using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Iterations.Dtos;

namespace Wayd.Planning.Application.Iterations.Queries;

public sealed record GetTeamActiveSprintQuery(Guid TeamId) : IQuery<SprintDetailsDto?>;

public sealed class GetTeamActiveSprintQueryValidator : CustomValidator<GetTeamActiveSprintQuery>
{
    public GetTeamActiveSprintQueryValidator()
    {
        RuleFor(q => q.TeamId)
            .NotEmpty();
    }
}

public sealed class GetTeamActiveSprintQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetTeamActiveSprintQuery, SprintDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<SprintDetailsDto?> Handle(GetTeamActiveSprintQuery request, CancellationToken cancellationToken)
    {
        var sprint = await _planningDbContext.Iterations
            .Where(i => i.TeamId == request.TeamId && i.Type == IterationType.Sprint && i.State == IterationState.Active)
            .ProjectToType<SprintDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return sprint;
    }
}
