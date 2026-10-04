using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Application.Iterations.Dtos;

public sealed record SimpleIterationDto : IMapFrom<Iteration>, ISimpleIteration
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public required string Name { get; init; }
    public IterationType Type { get; init; }
    public required IterationDateRange DateRange { get; init; }
    public Guid? TeamId { get; init; }
}
