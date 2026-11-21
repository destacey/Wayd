using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.Teams;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.AppIntegration.Application.Connections.Commands.Teams;

/// <summary>
/// Runs the hello-world Graph test for a Teams connection: resolves the signed-in user's email in
/// the tenant, creates a Teams chat, and creates a calendar event. Does not post a chat message.
/// </summary>
public sealed record TestTeamsConnectionCommand(Guid Id) : ICommand;

public sealed class TestTeamsConnectionCommandValidator : CustomValidator<TestTeamsConnectionCommand>
{
    public TestTeamsConnectionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class TestTeamsConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    ICurrentUser currentUser,
    ITeamsCommunicationGraphClient teamsCommunicationGraphClient)
    : ICommandHandler<TestTeamsConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ITeamsCommunicationGraphClient _teamsCommunicationGraphClient = teamsCommunicationGraphClient;

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

        return await _teamsCommunicationGraphClient.RunHelloWorldTestAsync(
            credentials,
            email.Trim(),
            cancellationToken);
    }
}
