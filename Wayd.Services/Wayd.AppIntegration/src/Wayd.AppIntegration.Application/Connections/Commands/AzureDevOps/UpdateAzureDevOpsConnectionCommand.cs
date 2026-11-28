using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;

/// <param name="PersonalAccessToken">Blank or masked keeps the stored token — see <see cref="ConnectionSecret.Replacement"/>.</param>
public sealed record UpdateAzureDevOpsConnectionCommand(Guid Id, string Name, string? Description, string Organization, string? PersonalAccessToken) : ICommand<Guid>;

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

        // Deliberately not NotEmpty: a blank token means "keep the stored one".
        RuleFor(c => c.PersonalAccessToken)
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

public sealed class UpdateAzureDevOpsConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<UpdateAzureDevOpsConnectionCommandHandler> logger, IAzureDevOpsService azureDevOpsService, IExternalSystemLinker externalSystemLinker) : ICommandHandler<UpdateAzureDevOpsConnectionCommand, Guid>
{
    private readonly IExternalSystemLinker _externalSystemLinker = externalSystemLinker;
    private const string AppRequestName = nameof(UpdateAzureDevOpsConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateAzureDevOpsConnectionCommandHandler> _logger = logger;
    private readonly IAzureDevOpsService _azureDevOpsService = azureDevOpsService;

    public async Task<Result<Guid>> Handle(UpdateAzureDevOpsConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (connection is null)
            return Result.Failure<Guid>("Azure DevOps connection not found.");

        var newPat = ConnectionSecret.Replacement(request.PersonalAccessToken, connection.Configuration!.PersonalAccessToken);

        // Before the connection test, which sends the token to the organization being saved.
        var credentialsResult = connection.CheckCredentialsFor(request.Organization, newPat);
        if (credentialsResult.IsFailure)
            return Result.Failure<Guid>(credentialsResult.Error);

        var config = new AzureDevOpsBoardsConnectionConfiguration(request.Organization, newPat ?? connection.Configuration.PersonalAccessToken);

        var systemIdAndTestResult = await _azureDevOpsService.GetSystemId(new AzureDevOpsConnectionContext(config.Organization, config.PersonalAccessToken), cancellationToken);
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

        var actor = EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId());
        var timestamp = _dateTimeProvider.Now;
        var organizationSystemId = systemIdAndTestResult.IsSuccess ? systemIdAndTestResult.Value : null;

        var updateResult = connection.Update(request.Name, request.Description, request.Organization, newPat, organizationSystemId, configurationIsValid, actor, timestamp);
        if (updateResult.IsFailure)
        {
            _logger.LogInformation("Wayd Request: Failure for Request {Name}.  Error message: {Error}", AppRequestName, updateResult.Error);
            return Result.Failure<Guid>(updateResult.Error);
        }

        var linkResult = await _externalSystemLinker.Link(connection, actor, timestamp, cancellationToken);
        if (linkResult.IsFailure)
            return Result.Failure<Guid>(linkResult.Error);

        var saveResult = await _externalSystemLinker.Save(cancellationToken);
        if (saveResult.IsFailure)
            return Result.Failure<Guid>(saveResult.Error);

        return Result.Success(connection.Id);
    }
}

