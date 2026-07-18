using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using NodaTime;

namespace Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;

public sealed record CreateAzureOpenAIConnectionCommand(string Name, string? Description, string ApiKey, string DeploymentName, string BaseUrl) : ICommand<Guid>;

public sealed class CreateAzureOpenAIConnectionCommandValidator : CustomValidator<CreateAzureOpenAIConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext;

    public CreateAzureOpenAIConnectionCommandValidator(IAppIntegrationDbContext appIntegrationDbContext)
    {
        _appIntegrationDbContext = appIntegrationDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.BaseUrl)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(c => c.ApiKey)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(c => c.DeploymentName)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync((command, deploymentName, cancellationToken) => BeUniqueAzureAIDeployment(command.BaseUrl, deploymentName, cancellationToken)).WithMessage("The deployment name for this connection already exists in an existing connection.");
    }

    /// <summary>
    /// Ensures that the combination of BaseUrl and DeploymentName is unique across existing Azure OpenAI connections.
    /// </summary>
    /// <param name="baseUrl"></param>
    /// <param name="deploymentName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> BeUniqueAzureAIDeployment(string baseUrl, string deploymentName, CancellationToken cancellationToken)
    {
        return await _appIntegrationDbContext.AzureOpenAIConnections
            .AllAsync(c => c.Configuration!.BaseUrl != baseUrl && c.Configuration!.DeploymentName != deploymentName, cancellationToken);
    }
}

public sealed class CreateAzureOpenAIConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateAzureOpenAIConnectionCommandHandler> logger) : ICommandHandler<CreateAzureOpenAIConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(CreateAzureOpenAIConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateAzureOpenAIConnectionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateAzureOpenAIConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            Instant timestamp = _dateTimeProvider.Now;
            var config = new AzureOpenAIConnectionConfiguration(request.ApiKey, request.DeploymentName, request.BaseUrl);

            // TODO: call some thing to Test the connection to then pass in the isValidConfiguration flag
            var isConfigurationValid = true;

            var connection = AzureOpenAIConnection.Create(request.Name, request.Description, config, isConfigurationValid, timestamp);

            await _appIntegrationDbContext.AzureOpenAIConnections.AddAsync(connection, cancellationToken);

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

