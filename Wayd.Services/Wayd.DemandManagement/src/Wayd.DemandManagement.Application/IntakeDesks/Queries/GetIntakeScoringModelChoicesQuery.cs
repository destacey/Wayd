using Wayd.Common.Domain.Scoring.Enums;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The active scoring models a desk can score its requests with, by name.
/// </summary>
/// <remarks>
/// Read here rather than from scoring model administration, whose endpoints need a permission a Desk Admin
/// usually does not hold. Building and editing models stays in Settings.
/// </remarks>
public sealed record GetIntakeScoringModelChoicesQuery : IQuery<List<IntakeScoringModelChoiceDto>>;

public sealed class GetIntakeScoringModelChoicesQueryHandler(IDemandManagementDbContext dbContext)
    : IQueryHandler<GetIntakeScoringModelChoicesQuery, List<IntakeScoringModelChoiceDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;

    public Task<List<IntakeScoringModelChoiceDto>> Handle(GetIntakeScoringModelChoicesQuery request, CancellationToken cancellationToken) =>
        _dbContext.ScoringModels
            .Where(m => m.State == ScoringModelState.Active)
            .OrderBy(m => m.Name).ThenBy(m => m.Key)
            .Select(m => new IntakeScoringModelChoiceDto { Id = m.Id, Key = m.Key, Name = m.Name, Description = m.Description })
            .ToListAsync(cancellationToken);
}
