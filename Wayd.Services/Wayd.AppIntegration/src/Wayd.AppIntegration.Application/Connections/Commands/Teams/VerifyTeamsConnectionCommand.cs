using FluentValidation;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.AppIntegration.Application.Connections.Commands.Teams;

/// <summary>
/// Runs the hello-world Graph test using credentials from the create form before a connection row exists.
/// </summary>
public sealed record VerifyTeamsConnectionCommand(
    string TenantId,
    string ClientId,
    string ClientSecret) : ICommand;

public sealed class VerifyTeamsConnectionCommandValidator : CustomValidator<VerifyTeamsConnectionCommand>
{
    public VerifyTeamsConnectionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.TenantId).NotEmpty().MaximumLength(64);
        RuleFor(c => c.ClientId).NotEmpty().MaximumLength(64);
        RuleFor(c => c.ClientSecret).NotEmpty().MaximumLength(512);
    }
}

public sealed class VerifyTeamsConnectionCommandHandler(
    ICurrentUser currentUser,
    ITeamsCommunicationGraphClient teamsCommunicationGraphClient,
    ILogger<VerifyTeamsConnectionCommandHandler> logger)
    : ICommandHandler<VerifyTeamsConnectionCommand>
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ITeamsCommunicationGraphClient _teamsCommunicationGraphClient = teamsCommunicationGraphClient;
    private readonly ILogger<VerifyTeamsConnectionCommandHandler> _logger = logger;

    public async Task<Result> Handle(VerifyTeamsConnectionCommand request, CancellationToken cancellationToken)
    {
        var email = _currentUser.GetUserEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure(
                "Your Wayd account has no email address; the connection test addresses you by that email.");
        }

        var credentials = new TeamsGraphCredentials(
            request.TenantId.Trim(),
            request.ClientId.Trim(),
            request.ClientSecret.Trim());

        try
        {
            return await _teamsCommunicationGraphClient.RunHelloWorldTestAsync(
                credentials,
                email.Trim(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", nameof(VerifyTeamsConnectionCommandHandler));
            return Result.Failure($"Failed to run the Teams connection test: {ex.Message}");
        }
    }
}
