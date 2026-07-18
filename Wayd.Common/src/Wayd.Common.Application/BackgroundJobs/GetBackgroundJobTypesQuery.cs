namespace Wayd.Common.Application.BackgroundJobs;

public sealed record GetBackgroundJobTypesQuery : IQuery<IReadOnlyList<BackgroundJobTypeDto>> { }

public sealed class GetBackgroundJobTypesQueryHandler : IQueryHandler<GetBackgroundJobTypesQuery, IReadOnlyList<BackgroundJobTypeDto>>
{
    public Task<IReadOnlyList<BackgroundJobTypeDto>> Handle(GetBackgroundJobTypesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<BackgroundJobTypeDto> values = Enum.GetValues<BackgroundJobType>().Select(c => new BackgroundJobTypeDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder(),
            GroupName = c.GetDisplayGroupName() ?? "Other"
        }).ToList();

        return Task.FromResult(values);
    }
}
