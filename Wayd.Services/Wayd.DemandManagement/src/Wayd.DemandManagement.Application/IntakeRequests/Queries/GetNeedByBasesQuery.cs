using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The need-by basis values, each with its code and family, for a client to build its options from.
/// </summary>
public sealed record GetNeedByBasesQuery : IQuery<List<NeedByBasisDto>>;

public sealed class GetNeedByBasesQueryHandler : IQueryHandler<GetNeedByBasesQuery, List<NeedByBasisDto>>
{
    public Task<List<NeedByBasisDto>> Handle(GetNeedByBasesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(CommonEnumDto<NeedByBasis>.GetValues<NeedByBasisDto>());
}

/// <summary>
/// One need-by basis: its code, its name, and whether it is fixed for us or chosen by us.
/// </summary>
public sealed record NeedByBasisDto : CommonEnumDto<NeedByBasis>
{
    /// <summary>Whether something outside the company sets the date or someone here chose it.</summary>
    public NeedByBasisFamily Family => Code.Family();

    /// <summary>Whether a need-by on this basis also records who made the commitment and when.</summary>
    public bool RecordsCommitment => Code.RecordsCommitment();
}
