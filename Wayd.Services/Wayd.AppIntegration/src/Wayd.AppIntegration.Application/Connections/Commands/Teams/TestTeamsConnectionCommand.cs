using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.Teams;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.AppIntegration.Application.Connections.Commands.Teams;

/// <summary>
/// Runs the hello-world Graph test for a Teams connection: resolves the signed-in user's email in
/// the tenant, creates a Teams chat, and a calendar event. Does not post a chat message.
/// </summary>
public sealed record TestTeamsConnectionCommand(Guid Id, string UserAssertion) : ICommand;

public sealed class TestTeamsConnectionCommandValidator : CustomValidator<TestTeamsConnectionCommand>
{
    public TestTeamsConnectionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.UserAssertion)
            .NotEmpty()
            .WithMessage(TeamsConnectionTestTokens.MissingUserAssertionMessage);
    }
}

public sealed class TestTeamsConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    ICurrentUser currentUser,
    ITeamsCommunicationGraphClient teamsCommunicationGraphClient,
    ILogger<TestTeamsConnectionCommandHandler> logger)
    : ICommandHandler<TestTeamsConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ITeamsCommunicationGraphClient _teamsCommunicationGraphClient = teamsCommunicationGraphClient;
    private readonly ILogger<TestTeamsConnectionCommandHandler> _logger = logger;

    public async Task<Result> Handle(TestTeamsConnectionCommand request, CancellationToken cancellationToken)
    {
        var email = _currentUser.GetUserEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure(
                "Your Wayd account has no email address; the connection test addresses you by that email.");
        }

        var connection = await _appIntegrationDbContext.TeamsConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);

        if (connection is null)
            return Result.Failure($"Teams connection {request.Id} was not found.");

        var config = connection.Configuration;
        var credentials = new TeamsGraphCredentials(config.TenantId, config.ClientId, config.ClientSecret);

        try
        {
            return await _teamsCommunicationGraphClient.RunHelloWorldTestAsync(
                credentials,
                email.Trim(),
                request.UserAssertion.Trim(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {Id}", nameof(TestTeamsConnectionCommandHandler), request.Id);
            return Result.Failure($"Failed to run the Teams connection test: {ex.Message}");
        }
    }
}
