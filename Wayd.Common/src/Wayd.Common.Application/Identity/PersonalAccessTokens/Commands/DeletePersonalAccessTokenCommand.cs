using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Identity.PersonalAccessTokens.Commands;

/// <summary>
/// Command to permanently delete a personal access token.
/// Unlike revoke, this removes the token from the database entirely.
/// </summary>
public sealed record DeletePersonalAccessTokenCommand(Guid TokenId) : ICommand;

public sealed class DeletePersonalAccessTokenCommandValidator : CustomValidator<DeletePersonalAccessTokenCommand>
{
    public DeletePersonalAccessTokenCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.TokenId)
            .NotEmpty();
    }
}

internal sealed class DeletePersonalAccessTokenCommandHandler(
    IWaydDbContext dbContext,
    ICurrentUser currentUser,
    ILogger<DeletePersonalAccessTokenCommandHandler> logger) : ICommandHandler<DeletePersonalAccessTokenCommand>
{
    private const string AppRequestName = nameof(DeletePersonalAccessTokenCommand);

    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<DeletePersonalAccessTokenCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeletePersonalAccessTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userIdString = _currentUser.GetUserId();

            var token = await _dbContext.PersonalAccessTokens
                .FirstOrDefaultAsync(t => t.Id == request.TokenId && t.UserId == userIdString, cancellationToken);

            if (token == null)
            {
                _logger.LogWarning(
                    "Attempt to delete personal access token failed. Token not found or no permission. UserId: {UserId}, TokenId: {TokenId}",
                    userIdString, request.TokenId);
                return Result.Failure("Token not found or you do not have permission to delete it.");
            }

            _dbContext.PersonalAccessTokens.Remove(token);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Personal access token deleted. UserId: {UserId}, TokenId: {TokenId}, TokenName: {TokenName}",
                userIdString, token.Id, token.Name);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<CreatePersonalAccessTokenResult>($"Error handling {AppRequestName} command.");
        }
    }
}
