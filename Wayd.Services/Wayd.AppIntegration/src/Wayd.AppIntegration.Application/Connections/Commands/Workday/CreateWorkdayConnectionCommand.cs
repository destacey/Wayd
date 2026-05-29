using FluentValidation;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using NodaTime;

namespace Wayd.AppIntegration.Application.Connections.Commands.Workday;

public sealed record CreateWorkdayConnectionCommand(
    string Name,
    string? Description,
    string WsdlUrl,
    string IsuUsername,
    string IsuPassword,
    WorkdayWorkerKey WorkerKey,
    bool IncludeInactive,
    bool IncrementalSyncEnabled,
    EmployeeMatchProperty MatchBy) : ICommand<Guid>;

public sealed class CreateWorkdayConnectionCommandValidator : CustomValidator<CreateWorkdayConnectionCommand>
{
    public CreateWorkdayConnectionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

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

internal sealed class CreateWorkdayConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IDateTimeProvider dateTimeProvider,
    IWorkdayConnectionInitializer initializer,
    ILogger<CreateWorkdayConnectionCommandHandler> logger)
    : ICommandHandler<CreateWorkdayConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(CreateWorkdayConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IWorkdayConnectionInitializer _initializer = initializer;
    private readonly ILogger<CreateWorkdayConnectionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateWorkdayConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            Instant timestamp = _dateTimeProvider.Now;

            var config = new WorkdayConnectionConfiguration(
                request.WsdlUrl,
                request.IsuUsername,
                request.IsuPassword,
                request.WorkerKey,
                request.IncludeInactive,
                request.IncrementalSyncEnabled,
                request.MatchBy);

            // Create the connection first with IsValidConfiguration = false so the row exists even
            // if the probe is slow or fails — admins shouldn't lose typed config.
            var connection = WorkdayConnection.Create(request.Name, request.Description, config, configurationIsValid: false, timestamp);
            await _appIntegrationDbContext.WorkdayConnections.AddAsync(connection, cancellationToken);
            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            // Run the init probe and persist the structured result.
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
            IncrementalUpdatedFrom: null);

        var result = await _initializer.Initialize(credentials, cancellationToken);
        connection.RecordInitResult(
            result.IsValid,
            result.MissingRequiredFields,
            result.Warnings,
            result.AuthError,
            DateTimeOffset.UtcNow);
    }
}
