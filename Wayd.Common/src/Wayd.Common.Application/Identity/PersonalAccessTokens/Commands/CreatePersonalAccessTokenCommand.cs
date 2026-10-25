using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Application.Identity.PersonalAccessTokens.Commands;

/// <summary>
/// Command to create a new personal access token.
/// </summary>
/// <param name="Name">The user-friendly name for the token, unique among the user's active tokens.</param>
/// <param name="ExpiresAt">When the token stops working.</param>
/// <param name="Scope">How much of the user's access the token may use.</param>
public sealed record CreatePersonalAccessTokenCommand(string Name, Instant ExpiresAt, TokenScope Scope = TokenScopes.Default) : ICommand<CreatePersonalAccessTokenResult>;

/// <summary>
/// The newly created personal access token, including its plaintext value.
/// This is the only time the plaintext token is returned; store it now, as it cannot be retrieved later.
/// </summary>
public sealed record CreatePersonalAccessTokenResult(Guid Id, string Name, string Token, Instant ExpiresAt, TokenScope Scope);

public sealed class CreatePersonalAccessTokenCommandValidator : CustomValidator<CreatePersonalAccessTokenCommand>
{
    private const int MaxTokensPerUser = 10;
    private const int MaxExpirationDays = 730; // 2 years

    private readonly IWaydDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreatePersonalAccessTokenCommandValidator(
        IWaydDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .MustAsync(BeUniqueTokenName)
                .WithMessage("You already have a token with this name.");

        RuleFor(x => x.Scope)
            .IsInEnum();

        RuleFor(x => x.ExpiresAt)
            .Must(BeValidExpirationDate)
                .WithMessage($"Expiration date must be between 1 day and {MaxExpirationDays} days ({MaxExpirationDays / 365} years) from now.");
    }

    private async Task<bool> BeUniqueTokenName(string name, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        return !await _dbContext.PersonalAccessTokens
            .AnyAsync(t => t.UserId == userId && t.Name == name && t.RevokedAt == null, cancellationToken);
    }

    private bool BeValidExpirationDate(Instant expiresAt)
    {
        var now = _dateTimeProvider.Now;
        var minExpiration = now.Plus(Duration.FromDays(1));
        var maxExpiration = now.Plus(Duration.FromDays(MaxExpirationDays));

        return expiresAt >= minExpiration && expiresAt <= maxExpiration;
    }
}

public sealed class CreatePersonalAccessTokenCommandHandler(
    IWaydDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ITokenHashingService tokenHashingService,
    ILogger<CreatePersonalAccessTokenCommandHandler> logger) : ICommandHandler<CreatePersonalAccessTokenCommand, CreatePersonalAccessTokenResult>
{
    private const string AppRequestName = nameof(CreatePersonalAccessTokenCommand);

    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ITokenHashingService _tokenHashingService = tokenHashingService;
    private readonly ILogger<CreatePersonalAccessTokenCommandHandler> _logger = logger;

    public async Task<Result<CreatePersonalAccessTokenResult>> Handle(CreatePersonalAccessTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUser.GetUserId();
            var now = _dateTimeProvider.Now;

            // verify user hasn't exceeded token limit
            var activeTokenCount = await _dbContext.PersonalAccessTokens
                .CountAsync(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now, cancellationToken);
            if (activeTokenCount >= 10)
            {
                return Result.Failure<CreatePersonalAccessTokenResult>($"You have reached the maximum of 10 active tokens. Please revoke an existing token before creating a new one.");
            }

            // Generate the token
            var plaintextToken = _tokenHashingService.GenerateToken();
            var (tokenIdentifier, tokenHash) = _tokenHashingService.HashToken(plaintextToken);

            var tokenResult = PersonalAccessToken.Create(
                name: request.Name,
                tokenIdentifier: tokenIdentifier,
                tokenHash: tokenHash,
                userId: userId,
                expiresAt: request.ExpiresAt,
                scope: request.Scope,
                actor: EventActor.User(userId, _currentUser.GetEmployeeId()),
                timestamp: now
            );

            if (tokenResult.IsFailure)
            {
                return Result.Failure<CreatePersonalAccessTokenResult>(tokenResult.Error);
            }

            var token = tokenResult.Value;

            await _dbContext.PersonalAccessTokens.AddAsync(token, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Personal access token created. UserId: {UserId}, TokenId: {TokenId}, TokenName: {TokenName}, ExpiresAt: {ExpiresAt}, Scope: {Scope}",
                userId, token.Id, token.Name, token.ExpiresAt, token.Scope);

            // Return the result with the plaintext token (ONLY time it's visible!)
            return Result.Success(new CreatePersonalAccessTokenResult(token.Id, token.Name, plaintextToken, token.ExpiresAt, token.Scope));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<CreatePersonalAccessTokenResult>($"Error handling {AppRequestName} command.");
        }
    }
}
