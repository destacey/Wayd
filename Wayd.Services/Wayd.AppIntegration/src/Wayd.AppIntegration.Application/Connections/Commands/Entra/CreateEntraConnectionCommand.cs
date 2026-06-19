using FluentValidation;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Domain.Enums.AppIntegrations;
using NodaTime;

namespace Wayd.AppIntegration.Application.Connections.Commands.Entra;

public sealed record CreateEntraConnectionCommand(
    string Name,
    string? Description,
    string TenantId,
    string ClientId,
    string ClientSecret,
    string? AllUsersGroupObjectId,
    bool IncludeDisabledUsers,
    EmployeeMatchProperty MatchBy,
    bool NormalizeNameCasing) : ICommand<Guid>;

public sealed class CreateEntraConnectionCommandValidator : CustomValidator<CreateEntraConnectionCommand>
{
    public CreateEntraConnectionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

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

internal sealed class CreateEntraConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreateEntraConnectionCommandHandler> logger)
    : ICommandHandler<CreateEntraConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(CreateEntraConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateEntraConnectionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateEntraConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            Instant timestamp = _dateTimeProvider.Now;

            var config = new EntraConnectionConfiguration(
                request.TenantId,
                request.ClientId,
                request.ClientSecret,
                request.AllUsersGroupObjectId,
                request.IncludeDisabledUsers,
                request.MatchBy,
                request.NormalizeNameCasing);

            // TODO: Test the connection here and set IsValidConfiguration based on the outcome.
            var isConfigurationValid = true;

            var connection = EntraConnection.Create(request.Name, request.Description, config, isConfigurationValid, timestamp);

            await _appIntegrationDbContext.EntraConnections.AddAsync(connection, cancellationToken);

            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(connection.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", AppRequestName, request);
            return Result.Failure<Guid>($"Wayd Request: Exception for Request {AppRequestName} {request}");
        }
    }
}
