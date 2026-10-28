using FluentValidation;
using Wayd.AppIntegration.Application.Logging;
using Wayd.AppIntegration.Domain.Models.Teams;
using NodaTime;

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
    ILogger<CreateTeamsConnectionCommandHandler> logger)
    : ICommandHandler<CreateTeamsConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(CreateTeamsConnectionCommandHandler);

    public async Task<Result<Guid>> Handle(CreateTeamsConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = dateTimeProvider.Now;
            var config = new TeamsConnectionConfiguration(request.TenantId, request.ClientId, request.ClientSecret);
            var isConfigurationValid = true;

            var connection = TeamsConnection.Create(
                request.Name,
                request.Description,
                config,
                isConfigurationValid,
                EventActor.User(currentUser.GetUserId(), currentUser.GetEmployeeId()),
                timestamp);

            await appIntegrationDbContext.TeamsConnections.AddAsync(connection, cancellationToken);
            await appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(connection.Id);
        }
        catch (Exception ex)
        {
            var redactedRequest = request.Redact();
            logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", AppRequestName, redactedRequest);
            return Result.Failure<Guid>($"Wayd Request: Exception for Request {AppRequestName} {redactedRequest}");
        }
    }
}
