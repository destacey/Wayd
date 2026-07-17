using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;

namespace Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;

public sealed record UpdateAzureOpenAIConnectionCommand(Guid Id, string Name, string BaseUrl, string? Description, string DeploymentName, string ApiKey) : ICommand<Guid>;

public sealed class UpdateAzureOpenAIConnectionCommandValidator : CustomValidator<UpdateAzureOpenAIConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext;

    public UpdateAzureOpenAIConnectionCommandValidator(IAppIntegrationDbContext appIntegrationDbContext)
    {
        _appIntegrationDbContext = appIntegrationDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.DeploymentName)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(async (cmd, deploymentName, cancellationToken) => await BeUniqueAzureAIDeployment(cmd.BaseUrl, deploymentName, cancellationToken)).WithMessage("The deployment name for this connection already exists in an existing connection.");

        RuleFor(c => c.ApiKey)
            .NotEmpty()
            .MaximumLength(128);
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

public sealed class UpdateAzureOpenAIConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateAzureOpenAIConnectionCommandHandler> logger) : ICommandHandler<UpdateAzureOpenAIConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(UpdateAzureOpenAIConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateAzureOpenAIConnectionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(UpdateAzureOpenAIConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.AzureOpenAIConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
                return Result.Failure<Guid>("Azure OpenAI connection not found.");

            // do the first four characters of the ApiKey match the existing one?
            var apiKey = connection.Configuration!.ApiKey.Length == request.ApiKey.Length
                && connection.Configuration!.ApiKey?[..4] == request.ApiKey[..4]
                    ? connection.Configuration.ApiKey
                    : request.ApiKey;

            var config = new AzureOpenAIConnectionConfiguration(request.BaseUrl, request.DeploymentName, apiKey);

            // TODO: Check the connection validity via some service making a "Test message" chat call using the connection creds
            var configurationIsValid = true;

            var updateResult = connection.Update(request.Name, request.Description, apiKey, request.DeploymentName, configurationIsValid, _dateTimeProvider.Now);
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

