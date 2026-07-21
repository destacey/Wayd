using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Logging;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Commands.Entra;

public sealed record UpdateEntraConnectionCommand(
    Guid Id,
    string Name,
    string? Description,
    string TenantId,
    string ClientId,
    string ClientSecret,
    string? AllUsersGroupObjectId,
    bool IncludeDisabledUsers,
    EmployeeMatchProperty MatchBy,
    bool NormalizeNameCasing) : ICommand<Guid>;

public sealed class UpdateEntraConnectionCommandValidator : CustomValidator<UpdateEntraConnectionCommand>
{
    public UpdateEntraConnectionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.TenantId)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.ClientId)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.ClientSecret)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(c => c.AllUsersGroupObjectId)
            .MaximumLength(64);
    }
}

public sealed class UpdateEntraConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<UpdateEntraConnectionCommandHandler> logger)
    : ICommandHandler<UpdateEntraConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(UpdateEntraConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateEntraConnectionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(UpdateEntraConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.EntraConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
                return Result.Failure<Guid>("Entra connection not found.");

            // If the first four characters of the ClientSecret match the existing one, preserve the existing
            // value (the API masks the secret on read and the client may post the masked or partially shown value).
            var clientSecret = connection.Configuration!.ClientSecret.Length == request.ClientSecret.Length
                && connection.Configuration!.ClientSecret[..Math.Min(4, connection.Configuration.ClientSecret.Length)]
                    == request.ClientSecret[..Math.Min(4, request.ClientSecret.Length)]
                    ? connection.Configuration.ClientSecret
                    : request.ClientSecret;

            // TODO: Validate credentials with Graph and reflect in IsValidConfiguration.
            var configurationIsValid = true;

            var updateResult = connection.Update(
                request.Name,
                request.Description,
                request.TenantId,
                request.ClientId,
                clientSecret,
                request.AllUsersGroupObjectId,
                request.IncludeDisabledUsers,
                request.MatchBy,
                request.NormalizeNameCasing,
                configurationIsValid,
                _dateTimeProvider.Now);

            if (updateResult.IsFailure)
            {
                await _appIntegrationDbContext.Entry(connection).ReloadAsync(cancellationToken);
                connection.ClearDomainEvents();
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", AppRequestName, request.Redact(), updateResult.Error);
                return Result.Failure<Guid>(updateResult.Error);
            }

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
