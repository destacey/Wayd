using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The desk visibility values, each with its code, for a client to build its options from.
/// </summary>
public sealed record GetIntakeDeskVisibilitiesQuery : IQuery<List<IntakeDeskVisibilityDto>>;

public sealed class GetIntakeDeskVisibilitiesQueryHandler : IQueryHandler<GetIntakeDeskVisibilitiesQuery, List<IntakeDeskVisibilityDto>>
{
    public Task<List<IntakeDeskVisibilityDto>> Handle(GetIntakeDeskVisibilitiesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(CommonEnumDto<IntakeDeskVisibility>.GetValues<IntakeDeskVisibilityDto>());
}

/// <summary>
/// One desk visibility value: its code, name and what it exposes.
/// </summary>
public sealed record IntakeDeskVisibilityDto : CommonEnumDto<IntakeDeskVisibility> { }
