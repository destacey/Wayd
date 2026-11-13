using FluentValidation;
using Wayd.AppIntegration.Domain.Models.Teams;
using NodaTime;

namespace Wayd.AppIntegration.Application.Connections.Commands.Teams;

/// <summary>
/// Persists a Microsoft Teams connection. Does not run the communication test — that test creates
/// a Teams chat and a calendar event, and only <see cref="VerifyTeamsConnectionCommand"/> (create
/// form) and <see cref="TestTeamsConnectionCommand"/> (an existing connection) run it.
/// </summary>
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
    ILogger<CreateTeamsConnectionCommandHandler> logger)
    : ICommandHandler<CreateTeamsConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(CreateTeamsConnectionCommandHandler);
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateTeamsConnectionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateTeamsConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = _dateTimeProvider.Now;
            var config = new TeamsConnectionConfiguration(request.TenantId, request.ClientId, request.ClientSecret);

            var connection = TeamsConnection.Create(
                request.Name,
                request.Description,
                config,
                configurationIsValid: true,
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                timestamp);

            await _appIntegrationDbContext.TeamsConnections.AddAsync(connection, cancellationToken);
            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(connection.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", AppRequestName);
            return Result.Failure<Guid>($"Wayd Request: Exception for Request {AppRequestName}");
        }
    }
}
