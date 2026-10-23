using Wayd.Common.Application.Requests.WorkManagement.Interfaces;

namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>Gets a work process's schemes, one per work type; null when no work process has the id.</summary>
/// <param name="WorkProcessId">The work process's id.</param>
public sealed record GetWorkProcessSchemesQuery(Guid WorkProcessId) : IQuery<IReadOnlyList<IWorkProcessSchemeDto>?>;

public sealed class GetWorkProcessSchemesQueryValidator : CustomValidator<GetWorkProcessSchemesQuery>
{
    public GetWorkProcessSchemesQueryValidator()
    {
        RuleFor(q => q.WorkProcessId)
            .NotEmpty();
    }
}
