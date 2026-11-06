using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Logging;
using Wayd.AppIntegration.Domain.Models.Teams;

namespace Wayd.AppIntegration.Application.Connections.Commands.Teams;

public sealed record UpdateTeamsConnectionCommand(
    Guid Id,
    string Name,
    string? Description,
    string TenantId,
    string ClientId,
    string? ClientSecret) : ICommand<Guid>;

public sealed class UpdateTeamsConnectionCommandValidator : CustomValidator<UpdateTeamsConnectionCommand>
{
    public UpdateTeamsConnectionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).MaximumLength(1024);
        RuleFor(c => c.TenantId).NotEmpty().MaximumLength(64);
        RuleFor(c => c.ClientId).NotEmpty().MaximumLength(64);
        RuleFor(c => c.ClientSecret).MaximumLength(512);
    }
}

public sealed class UpdateTeamsConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<UpdateTeamsConnectionCommandHandler> logger)
    : ICommandHandler<UpdateTeamsConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(UpdateTeamsConnectionCommandHandler);

    public async Task<Result<Guid>> Handle(UpdateTeamsConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await appIntegrationDbContext.TeamsConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
                return Result.Failure<Guid>("Teams connection not found.");

            var clientSecret = ConnectionSecret.Resolve(request.ClientSecret, connection.Configuration!.ClientSecret);
            var configurationIsValid = true;

            var updateResult = connection.Update(
                request.Name,
                request.Description,
                request.TenantId,
                request.ClientId,
                clientSecret,
                configurationIsValid,
                EventActor.User(currentUser.GetUserId(), currentUser.GetEmployeeId()),
                dateTimeProvider.Now);

            if (updateResult.IsFailure)
            {
                await appIntegrationDbContext.Entry(connection).ReloadAsync(cancellationToken);
                connection.ClearDomainEvents();
                logger.LogError(
                    "Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}",
                    AppRequestName,
                    request.Redact(),
                    updateResult.Error);
                return Result.Failure<Guid>(updateResult.Error);
            }

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
