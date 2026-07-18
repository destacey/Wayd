using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;

public sealed record UpdateAzureDevOpsConnectionCommand(Guid Id, string Name, string? Description, string Organization, string PersonalAccessToken) : ICommand<Guid>;

public sealed class UpdateAzureDevOpsConnectionCommandValidator : CustomValidator<UpdateAzureDevOpsConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext;

    public UpdateAzureDevOpsConnectionCommandValidator(IAppIntegrationDbContext appIntegrationDbContext)
    {
        _appIntegrationDbContext = appIntegrationDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.Organization)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(async (cmd, organization, cancellationToken) => await BeUniqueOrganization(cmd.Id, organization, cancellationToken)).WithMessage("The organization for this connection already exists in an existing connection.");

        RuleFor(c => c.PersonalAccessToken)
            .NotEmpty()
            .MaximumLength(128);
    }

    public async Task<bool> BeUniqueOrganization(Guid id, string organization, CancellationToken cancellationToken)
    {
        var configurations = await _appIntegrationDbContext.AzureDevOpsBoardsConnections
            .Where(c => c.Connector == Connector.AzureDevOps && c.Id != id)
            .Select(c => c.Configuration)
            .ToListAsync(cancellationToken);

        return configurations.All(c => c.Organization != organization);
    }
}

public sealed class UpdateAzureDevOpsConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateAzureDevOpsConnectionCommandHandler> logger, IAzureDevOpsService azureDevOpsService) : ICommandHandler<UpdateAzureDevOpsConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(UpdateAzureDevOpsConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateAzureDevOpsConnectionCommandHandler> _logger = logger;
    private readonly IAzureDevOpsService _azureDevOpsService = azureDevOpsService;

    public async Task<Result<Guid>> Handle(UpdateAzureDevOpsConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
                return Result.Failure<Guid>("Azure DevOps connection not found.");

            // do the first four characters of the PersonalAccessToken match the existing one?
            var pat = connection.Configuration!.PersonalAccessToken.Length == request.PersonalAccessToken.Length
                && connection.Configuration!.PersonalAccessToken?[..4] == request.PersonalAccessToken[..4]
                    ? connection.Configuration.PersonalAccessToken
                    : request.PersonalAccessToken;


            var config = new AzureDevOpsBoardsConnectionConfiguration(request.Organization, pat);

            var systemIdAndTestResult = await _azureDevOpsService.GetSystemId(config.OrganizationUrl, config.PersonalAccessToken, cancellationToken);
            if (systemIdAndTestResult.IsFailure)
            {
                _logger.LogWarning("Unable to get system id for Azure DevOps connection for organization {Organization}. {Error}", request.Organization, systemIdAndTestResult.Error);
            }
            else if (string.IsNullOrWhiteSpace(connection.SystemId))
            {
                var setSystemIdResult = connection.SetSystemId(systemIdAndTestResult.Value);
                if (setSystemIdResult.IsFailure)
                {
                    _logger.LogError("Error setting system id for connection {ConnectionId} to {SystemId}. {Error}", connection.Id, systemIdAndTestResult.Value, setSystemIdResult.Error);
                }
            }

            var configurationIsValid = systemIdAndTestResult.IsSuccess && !string.IsNullOrWhiteSpace(connection.SystemId);

            var updateResult = connection.Update(request.Name, request.Description, request.Organization, pat, configurationIsValid, _dateTimeProvider.Now);
            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _appIntegrationDbContext.Entry(connection).ReloadAsync(cancellationToken);
                connection.ClearDomainEvents();

                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", AppRequestName, request, updateResult.Error);
                return Result.Failure<Guid>(updateResult.Error);
            }

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

