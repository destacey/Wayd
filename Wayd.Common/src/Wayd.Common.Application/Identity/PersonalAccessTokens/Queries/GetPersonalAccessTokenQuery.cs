using Wayd.Common.Application.Identity.PersonalAccessTokens.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Identity.PersonalAccessTokens.Queries;

/// <summary>
/// Query to get a single personal access token by ID.
/// </summary>
public sealed record GetPersonalAccessTokenQuery(Guid TokenId) : IQuery<Result<PersonalAccessTokenDto>>;

public sealed class GetPersonalAccessTokenQueryValidator : CustomValidator<GetPersonalAccessTokenQuery>
{
    public GetPersonalAccessTokenQueryValidator()
    {
        RuleFor(x => x.TokenId)
            .NotEmpty();
    }
}

internal sealed class GetPersonalAccessTokenQueryHandler(IWaydDbContext dbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetPersonalAccessTokenQuery, Result<PersonalAccessTokenDto>>
{
    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<PersonalAccessTokenDto>> Handle(GetPersonalAccessTokenQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        var now = _dateTimeProvider.Now;

        var token = await _dbContext.PersonalAccessTokens
            .Where(t => t.Id == request.TokenId && t.UserId == userId)
            .Select(PersonalAccessTokenDto.Projection(now))
            .FirstOrDefaultAsync(cancellationToken);

        if (token == null)
        {
            return Result.Failure<PersonalAccessTokenDto>("Token not found or you do not have permission to view it.");
        }

        return Result.Success(token);
    }
}
