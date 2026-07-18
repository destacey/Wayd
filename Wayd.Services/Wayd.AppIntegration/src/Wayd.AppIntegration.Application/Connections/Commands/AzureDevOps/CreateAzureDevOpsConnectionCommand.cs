using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.AppIntegrations;
using NodaTime;

namespace Wayd.AppIntegration.Application.Connections.Commands;

public sealed record CreateAzureDevOpsConnectionCommand(string Name, string? Description, string Organization, string PersonalAccessToken) : ICommand<Guid>;

public sealed class CreateAzureDevOpsConnectionCommandValidator : CustomValidator<CreateAzureDevOpsConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext;

    public CreateAzureDevOpsConnectionCommandValidator(IAppIntegrationDbContext appIntegrationDbContext)
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
            .MustAsync(BeUniqueOrganization).WithMessage("The organization for this connection already exists in an existing connection.");

        RuleFor(c => c.PersonalAccessToken)
            .NotEmpty()
            .MaximumLength(128);
    }

    public async Task<bool> BeUniqueOrganization(string organization, CancellationToken cancellationToken)
    {
        var configurations = await _appIntegrationDbContext.AzureDevOpsBoardsConnections
            .Where(c => c.Connector == Connector.AzureDevOps)
            .Select(c => c.Configuration)
            .ToListAsync(cancellationToken);

        return configurations.All(c => c.Organization != organization);
    }
}

public sealed class CreateAzureDevOpsConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateAzureDevOpsConnectionCommandHandler> logger, IAzureDevOpsService azureDevOpsService) : ICommandHandler<CreateAzureDevOpsConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(CreateAzureDevOpsConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateAzureDevOpsConnectionCommandHandler> _logger = logger;
    private readonly IAzureDevOpsService _azureDevOpsService = azureDevOpsService;

    public async Task<Result<Guid>> Handle(CreateAzureDevOpsConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            Instant timestamp = _dateTimeProvider.Now;
            var config = new AzureDevOpsBoardsConnectionConfiguration(request.Organization, request.PersonalAccessToken);

            var systemIdResult = await _azureDevOpsService.GetSystemId(config.OrganizationUrl, config.PersonalAccessToken, cancellationToken);
            if (systemIdResult.IsFailure)
            {
                _logger.LogWarning("Unable to get system id for Azure DevOps connection for organization {Organization}. {Error}", request.Organization, systemIdResult.Error);
            }
            var systemId = systemIdResult.IsSuccess ? systemIdResult.Value : null;

            var connection = AzureDevOpsBoardsConnection.Create(request.Name, request.Description, systemId, config, systemIdResult.IsSuccess, null, timestamp);

            await _appIntegrationDbContext.AzureDevOpsBoardsConnections.AddAsync(connection, cancellationToken);

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

