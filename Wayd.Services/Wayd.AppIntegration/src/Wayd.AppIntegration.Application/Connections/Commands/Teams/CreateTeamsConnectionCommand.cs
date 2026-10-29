using FluentValidation;
using Wayd.AppIntegration.Application.Logging;
using Wayd.AppIntegration.Domain.Models.Teams;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.AppIntegration.Application.Connections.Commands.Teams;

public sealed record CreateTeamsConnectionCommand(
    string Name,
    string? Description,
    string TenantId,
    string ClientId,
    string ClientSecret) : ICommand<Guid>;

public sealed class CreateTeamsConnectionCommandValidator : CustomValidator<CreateTeamsConnectionCommand>
{
    public CreateTeamsConnectionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).MaximumLength(1024);
        RuleFor(c => c.TenantId).NotEmpty().MaximumLength(64);
        RuleFor(c => c.ClientId).NotEmpty().MaximumLength(64);
        RuleFor(c => c.ClientSecret).NotEmpty().MaximumLength(512);
    }
}

public sealed class CreateTeamsConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ITeamsCommunicationGraphClient teamsCommunicationGraphClient,
    ILogger<CreateTeamsConnectionCommandHandler> logger)
    : ICommandHandler<CreateTeamsConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(CreateTeamsConnectionCommandHandler);
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ITeamsCommunicationGraphClient _teamsCommunicationGraphClient = teamsCommunicationGraphClient;
    private readonly ILogger<CreateTeamsConnectionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateTeamsConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var email = _currentUser.GetUserEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                return Result.Failure<Guid>(
                    "Your Wayd account has no email address; verify the connection before saving.");
            }

            var credentials = new TeamsGraphCredentials(
                request.TenantId.Trim(),
                request.ClientId.Trim(),
                request.ClientSecret.Trim());

            var verification = await _teamsCommunicationGraphClient.RunHelloWorldTestAsync(
                credentials,
                email.Trim(),
                cancellationToken);
            if (verification.IsFailure)
                return Result.Failure<Guid>(verification.Error);

            var timestamp = _dateTimeProvider.Now;
            var config = new TeamsConnectionConfiguration(request.TenantId, request.ClientId, request.ClientSecret);
            var isConfigurationValid = true;

            var connection = TeamsConnection.Create(
                request.Name,
                request.Description,
                config,
                isConfigurationValid,
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                timestamp);

            await _appIntegrationDbContext.TeamsConnections.AddAsync(connection, cancellationToken);
            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(connection.Id);
        }
        catch (Exception ex)
        {
            var redactedRequest = request.Redact();
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", AppRequestName, redactedRequest);
            return Result.Failure<Guid>($"Wayd Request: Exception for Request {AppRequestName} {redactedRequest}");
        }
    }
}
