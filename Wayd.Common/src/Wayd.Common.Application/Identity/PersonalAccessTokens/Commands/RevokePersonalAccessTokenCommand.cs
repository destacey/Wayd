using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Identity.PersonalAccessTokens.Commands;

/// <summary>
/// Command to revoke a personal access token.
/// Revoked tokens are kept in the database for audit purposes.
/// </summary>
public sealed record RevokePersonalAccessTokenCommand(Guid TokenId) : ICommand;

public sealed class RevokePersonalAccessTokenCommandValidator : CustomValidator<RevokePersonalAccessTokenCommand>
{
    public RevokePersonalAccessTokenCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.TokenId)
            .NotEmpty();
    }
}

internal sealed class RevokePersonalAccessTokenCommandHandler(
    IWaydDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<RevokePersonalAccessTokenCommandHandler> logger) : ICommandHandler<RevokePersonalAccessTokenCommand>
{
    private const string AppRequestName = nameof(RevokePersonalAccessTokenCommand);

    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RevokePersonalAccessTokenCommandHandler> _logger = logger;

    public async Task<Result> Handle(RevokePersonalAccessTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUser.GetUserId();

            var token = await _dbContext.PersonalAccessTokens
                .FirstOrDefaultAsync(t => t.Id == request.TokenId && t.UserId == userId, cancellationToken);

            if (token == null)
            {
                _logger.LogWarning(
                    "Attempt to revoke personal access token failed. Token not found or no permission. UserId: {UserId}, TokenId: {TokenId}",
                    userId, request.TokenId);
                return Result.Failure("Token not found or you do not have permission to revoke it.");
            }

            var revokeResult = token.Revoke(userId, _dateTimeProvider.Now);
            if (revokeResult.IsFailure)
            {
                return revokeResult;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Personal access token revoked. UserId: {UserId}, TokenId: {TokenId}, TokenName: {TokenName}",
                userId, token.Id, token.Name);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<CreatePersonalAccessTokenResult>($"Error handling {AppRequestName} command.");
        }
    }
}
