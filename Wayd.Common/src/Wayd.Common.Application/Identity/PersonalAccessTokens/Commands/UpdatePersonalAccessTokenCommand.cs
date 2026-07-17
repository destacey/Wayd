using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Identity.PersonalAccessTokens.Commands;

/// <summary>
/// Command to update a personal access token's name and/or expiration date.
/// </summary>
public sealed record UpdatePersonalAccessTokenCommand(Guid TokenId, string Name, Instant ExpiresAt) : ICommand;

public sealed class UpdatePersonalAccessTokenCommandValidator : CustomValidator<UpdatePersonalAccessTokenCommand>
{
    private const int MaxExpirationDays = 730; // 2 years

    private readonly IWaydDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdatePersonalAccessTokenCommandValidator(
        IWaydDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.TokenId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .MustAsync(BeUniqueTokenName)
                .WithMessage("You already have a token with this name.");

        RuleFor(x => x.ExpiresAt)
            .Must(BeValidExpirationDate)
                .WithMessage($"Expiration date must be between 1 day and {MaxExpirationDays} days ({MaxExpirationDays / 365} years) from now.");
    }

    private async Task<bool> BeUniqueTokenName(UpdatePersonalAccessTokenCommand command, string name, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        return !await _dbContext.PersonalAccessTokens
            .AnyAsync(t => t.UserId == userId && t.Name == name && t.RevokedAt == null && t.Id != command.TokenId, cancellationToken);
    }

    private bool BeValidExpirationDate(Instant expiresAt)
    {
        var now = _dateTimeProvider.Now;
        var minExpiration = now.Plus(Duration.FromDays(1));
        var maxExpiration = now.Plus(Duration.FromDays(MaxExpirationDays));

        return expiresAt >= minExpiration && expiresAt <= maxExpiration;
    }
}

public sealed class UpdatePersonalAccessTokenCommandHandler(
    IWaydDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<UpdatePersonalAccessTokenCommandHandler> logger) : ICommandHandler<UpdatePersonalAccessTokenCommand>
{
    private const string AppRequestName = nameof(UpdatePersonalAccessTokenCommand);

    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdatePersonalAccessTokenCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdatePersonalAccessTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userIdString = _currentUser.GetUserId();
            var now = _dateTimeProvider.Now;

            var token = await _dbContext.PersonalAccessTokens
                .FirstOrDefaultAsync(t => t.Id == request.TokenId && t.UserId == userIdString, cancellationToken);

            if (token == null)
            {
                _logger.LogWarning(
                    "Attempt to update personal access token failed. Token not found or no permission. UserId: {UserId}, TokenId: {TokenId}",
                    userIdString, request.TokenId);
                return Result.Failure("Token not found or you do not have permission to update it.");
            }

            // Update name if it has changed
            if (token.Name != request.Name)
            {
                var nameUpdateResult = token.UpdateName(request.Name, now);
                if (nameUpdateResult.IsFailure)
                {
                    return nameUpdateResult;
                }
            }

            // Update expiration if it has changed
            if (token.ExpiresAt != request.ExpiresAt)
            {
                var expirationUpdateResult = token.UpdateExpiresAt(request.ExpiresAt, now);
                if (expirationUpdateResult.IsFailure)
                {
                    return expirationUpdateResult;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Personal access token updated. UserId: {UserId}, TokenId: {TokenId}, TokenName: {TokenName}, ExpiresAt: {ExpiresAt}",
                userIdString, token.Id, token.Name, token.ExpiresAt);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
