using Wayd.Common.Application.Identity.PersonalAccessTokens.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Identity.PersonalAccessTokens.Queries;

/// <summary>
/// Query to get all personal access tokens for the current user.
/// </summary>
public sealed record GetMyPersonalAccessTokensQuery : IQuery<Result<List<PersonalAccessTokenDto>>>;

internal sealed class GetMyPersonalAccessTokensQueryHandler(IWaydDbContext dbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetMyPersonalAccessTokensQuery, Result<List<PersonalAccessTokenDto>>>
{
    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<List<PersonalAccessTokenDto>>> Handle(GetMyPersonalAccessTokensQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        var now = _dateTimeProvider.Now;

        var tokens = await _dbContext.PersonalAccessTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.ExpiresAt)
            .Select(PersonalAccessTokenDto.Projection(now))
            .ToListAsync(cancellationToken);

        return Result.Success(tokens);
    }
}
