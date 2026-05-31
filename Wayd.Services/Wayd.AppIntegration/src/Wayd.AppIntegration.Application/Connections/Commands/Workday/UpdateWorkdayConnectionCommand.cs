using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Commands.Workday;

public sealed record UpdateWorkdayConnectionCommand(
    Guid Id,
    string Name,
    string? Description,
    string WsdlUrl,
    string IsuUsername,
    string IsuPassword,
    WorkdayWorkerKey WorkerKey,
    bool IncludeInactive,
    EmployeeMatchProperty MatchBy,
    bool UseUserIdAsEmailFallback,
    bool UsePreferredName,
    bool NormalizeNameCasing) : ICommand<Guid>;

public sealed class UpdateWorkdayConnectionCommandValidator : CustomValidator<UpdateWorkdayConnectionCommand>
{
    public UpdateWorkdayConnectionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).MaximumLength(1024);
        RuleFor(c => c.WsdlUrl)
            .NotEmpty()
            .MaximumLength(1024)
            .Must(BeParseable).WithMessage("WsdlUrl must be a Workday Staffing endpoint URL of the form https://{host}/ccx/service/{tenant}/Staffing/{version}.");
        RuleFor(c => c.IsuUsername).NotEmpty().MaximumLength(256);
        RuleFor(c => c.IsuPassword).NotEmpty().MaximumLength(512);
    }

    private static bool BeParseable(string url) => WorkdayConnectionConfiguration.TryParse(url, out _);
}

internal sealed class UpdateWorkdayConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IDateTimeProvider dateTimeProvider,
    IWorkdayConnectionInitializer initializer,
    ILogger<UpdateWorkdayConnectionCommandHandler> logger)
    : ICommandHandler<UpdateWorkdayConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(UpdateWorkdayConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IWorkdayConnectionInitializer _initializer = initializer;
    private readonly ILogger<UpdateWorkdayConnectionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(UpdateWorkdayConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.WorkdayConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
                return Result.Failure<Guid>("Workday connection not found.");

            // If the first four characters of the IsuPassword match the existing one, preserve the
            // existing value (the API masks the secret on read and the client posts back the masked
            // value when the user didn't change it).
            var isuPassword = connection.Configuration!.IsuPassword.Length == request.IsuPassword.Length
                && connection.Configuration!.IsuPassword[..Math.Min(4, connection.Configuration.IsuPassword.Length)]
                    == request.IsuPassword[..Math.Min(4, request.IsuPassword.Length)]
                    ? connection.Configuration.IsuPassword
                    : request.IsuPassword;

            // Apply the change set with a placeholder configurationIsValid; the init probe right
            // after this overwrites that value with the real outcome.
            var updateResult = connection.Update(
                request.Name,
                request.Description,
                request.WsdlUrl,
                request.IsuUsername,
                isuPassword,
                request.WorkerKey,
                request.IncludeInactive,
                request.MatchBy,
                request.UseUserIdAsEmailFallback,
                request.UsePreferredName,
                request.NormalizeNameCasing,
                configurationIsValid: false,
                _dateTimeProvider.Now);

            if (updateResult.IsFailure)
            {
                await _appIntegrationDbContext.Entry(connection).ReloadAsync(cancellationToken);
                connection.ClearDomainEvents();
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", AppRequestName, request, updateResult.Error);
                return Result.Failure<Guid>(updateResult.Error);
            }

            // Persist the change-set first so a slow/failing probe doesn't roll back the typed config.
            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            await RunInitProbe(connection, cancellationToken);
            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(connection.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", AppRequestName, request);
            return Result.Failure<Guid>($"Wayd Request: Exception for Request {AppRequestName} {request}");
        }
    }

    private async Task RunInitProbe(WorkdayConnection connection, CancellationToken cancellationToken)
    {
        var credentials = new WorkdayConnectionCredentials(
            connection.Configuration.SoapEndpoint,
            connection.Configuration.TenantAlias,
            connection.Configuration.WsdlVersion,
            connection.Configuration.IsuUsername,
            connection.Configuration.IsuPassword,
            connection.Configuration.WorkerKey,
            connection.Configuration.IncludeInactive,
            IncrementalUpdatedFrom: null,
            UseUserIdAsEmailFallback: connection.Configuration.UseUserIdAsEmailFallback,
            UsePreferredName: connection.Configuration.UsePreferredName,
            NormalizeNameCasing: connection.Configuration.NormalizeNameCasing);

        var result = await _initializer.Initialize(credentials, cancellationToken);
        connection.RecordInitResult(
            result.IsValid,
            result.MissingRequiredFields,
            result.Warnings,
            result.AuthError,
            DateTimeOffset.UtcNow);
    }
}
