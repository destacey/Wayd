using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The need-by basis families, each with its code and name, so a client can group and label the bases
/// without hard-coding what "fixed for us" and "we chose it" are called.
/// </summary>
public sealed record GetNeedByBasisFamiliesQuery : IQuery<List<NeedByBasisFamilyDto>>;

public sealed class GetNeedByBasisFamiliesQueryHandler : IQueryHandler<GetNeedByBasisFamiliesQuery, List<NeedByBasisFamilyDto>>
{
    public Task<List<NeedByBasisFamilyDto>> Handle(GetNeedByBasisFamiliesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(CommonEnumDto<NeedByBasisFamily>.GetValues<NeedByBasisFamilyDto>());
}

/// <summary>
/// One need-by basis family: its code, its name, and what it means for the date.
/// </summary>
public sealed record NeedByBasisFamilyDto : CommonEnumDto<NeedByBasisFamily> { }
