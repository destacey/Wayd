using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wayd.Infrastructure.Auth.PersonalAccessToken;

/// <summary>
/// Authentication handler for Personal Access Tokens (PATs).
/// Checks the x-api-key header for valid tokens.
/// Only attempts authentication when the x-api-key header is present to avoid
/// unnecessary logging when users authenticate via other schemes (e.g., JWT/AzureAD).
/// </summary>
public class PersonalAccessTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly WaydDbContext _dbContext;
    private readonly ITokenHashingService _tokenHashingService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PersonalAccessTokenAuthenticationHandler> _logger;

    public PersonalAccessTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        WaydDbContext dbContext,
        ITokenHashingService tokenHashingService,
        IDateTimeProvider dateTimeProvider,
        IServiceProvider serviceProvider)
        : base(options, loggerFactory, encoder)
    {
        _dbContext = dbContext;
        _tokenHashingService = tokenHashingService;
        _dateTimeProvider = dateTimeProvider;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<PersonalAccessTokenAuthenticationHandler>();

    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if the x-api-key header is present
        // Return NoResult immediately if not - this prevents unnecessary logging
        // about the scheme not being authenticated when users are using JWT/AzureAD
        if (!Request.Headers.TryGetValue(AuthConstants.ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedToken = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            _logger.LogWarning("Personal access token header present but empty");
            return AuthenticateResult.Fail("API key header present but empty.");
        }

        try
        {
            // Extract the token identifier (first 8 characters) for efficient lookup
            string tokenIdentifier = providedToken.Length >= 8
                ? providedToken.Substring(0, 8)
                : providedToken;

            var now = _dateTimeProvider.Now;

            // Efficient lookup: only get tokens with matching identifier
            var potentialTokens = await _dbContext.PersonalAccessTokens
                .Where(t => t.TokenIdentifier == tokenIdentifier
                         && t.RevokedAt == null
                         && t.ExpiresAt > now)
                .AsNoTracking()
                .ToListAsync();

            // Find the matching token by verifying the full hash
            var matchingToken = potentialTokens
                .FirstOrDefault(t => _tokenHashingService.VerifyToken(providedToken, t.TokenIdentifier, t.TokenHash));

            if (matchingToken == null)
            {
                _logger.LogWarning("Personal access token not found or invalid");
                return AuthenticateResult.Fail("Invalid or expired token.");
            }

            // Validate the token
            var validationResult = matchingToken.ValidateForUse(now);
            if (validationResult.IsFailure)
            {
                _logger.LogWarning("Personal access token validation failed: {Error}", validationResult.Error);
                return AuthenticateResult.Fail(validationResult.Error);
            }

            // Load the associated user
            var user = await _dbContext.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Id == matchingToken.UserId);

            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("User not found or inactive for token. UserId: {UserId}", matchingToken.UserId);
                return AuthenticateResult.Fail("User not found or inactive.");
            }

            // Update last used timestamp only if it's been more than 1 hour since last use
            // This reduces database writes by ~99% while still providing useful tracking
            var hoursSinceLastUse = matchingToken.LastUsedAt.HasValue
                ? (now - matchingToken.LastUsedAt.Value).TotalHours
                : double.MaxValue;

            if (hoursSinceLastUse >= 1.0)
            {
                // Use a separate scoped DbContext for the background update to avoid tracking conflicts
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<WaydDbContext>();
                        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

                        var token = await dbContext.PersonalAccessTokens
                            .FirstOrDefaultAsync(t => t.Id == matchingToken.Id);

                        if (token != null)
                        {
                            token.UpdateLastUsed(dateTimeProvider.Now);
                            await dbContext.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update last used timestamp for token {TokenId}", matchingToken.Id);
                    }
                });
            }

            // Build claims principal
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? "Unknown"),
                new Claim("AuthenticationType", "PersonalAccessToken"),
                new Claim("TokenId", matchingToken.Id.ToString())
            };

            // Only add email claim if email exists
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            if (matchingToken.EmployeeId.HasValue)
            {
                claims.Add(new Claim("EmployeeId", matchingToken.EmployeeId.Value.ToString()));
            }

            // TODO: In Phase 2, filter claims based on token scopes
            // For now, the user gets all their normal permissions

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Personal access token authentication succeeded. UserId: {UserId}, UserEmail: {UserEmail}, TokenId: {TokenId}, TokenName: {TokenName}",
                    user.Id, user.Email, matchingToken.Id, matchingToken.Name);
            }

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during personal access token authentication");
            return AuthenticateResult.Fail($"Authentication failed: {ex.Message}");
        }
    }
}
