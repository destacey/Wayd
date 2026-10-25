using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The roles a desk member can hold, each with its code, for a client to build its options from.
/// </summary>
public sealed record GetIntakeDeskRolesQuery : IQuery<List<IntakeDeskRoleDto>>;

public sealed class GetIntakeDeskRolesQueryHandler : IQueryHandler<GetIntakeDeskRolesQuery, List<IntakeDeskRoleDto>>
{
    public Task<List<IntakeDeskRoleDto>> Handle(GetIntakeDeskRolesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(CommonEnumDto<IntakeDeskRole>.GetValues<IntakeDeskRoleDto>());
}

/// <summary>
/// One desk role: its code, name and what it may do.
/// </summary>
public sealed record IntakeDeskRoleDto : CommonEnumDto<IntakeDeskRole> { }
