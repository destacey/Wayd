using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;

namespace Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;

/// <param name="ApiKey">Blank or masked keeps the stored key — see <see cref="ConnectionSecret.Replacement"/>.</param>
public sealed record UpdateAzureOpenAIConnectionCommand(Guid Id, string Name, string BaseUrl, string? Description, string DeploymentName, string? ApiKey) : ICommand<Guid>;

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

        RuleFor(c => c.BaseUrl)
            .NotEmpty()
            .MaximumLength(256)
            .Must(AzureOpenAIConnectionConfiguration.IsValidBaseUrl).WithMessage("BaseUrl must be an absolute HTTPS URL.");

        RuleFor(c => c.DeploymentName)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(async (cmd, deploymentName, cancellationToken) => await BeUniqueAzureAIDeployment(cmd.Id, cmd.BaseUrl, deploymentName, cancellationToken)).WithMessage("The deployment name for this connection already exists in an existing connection.");

        // Deliberately not NotEmpty: a blank key means "keep the stored one".
        RuleFor(c => c.ApiKey)
            .MaximumLength(128);
    }

    /// <summary>
    /// Ensures that the combination of BaseUrl and DeploymentName is unique across the other Azure OpenAI connections.
    /// </summary>
    /// <param name="id">The connection being updated, which may keep its own combination.</param>
    /// <param name="baseUrl"></param>
    /// <param name="deploymentName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> BeUniqueAzureAIDeployment(Guid id, string baseUrl, string deploymentName, CancellationToken cancellationToken)
    {
        // Compared in memory: the configuration is one encrypted column, which SQL cannot look inside.
        var configurations = await _appIntegrationDbContext.AzureOpenAIConnections
            .Where(c => c.Id != id)
            .Select(c => c.Configuration)
            .ToListAsync(cancellationToken);

        return !configurations.Any(c => c.IsSameDeployment(baseUrl, deploymentName));
    }
}

public sealed class UpdateAzureOpenAIConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<UpdateAzureOpenAIConnectionCommandHandler> logger) : ICommandHandler<UpdateAzureOpenAIConnectionCommand, Guid>
{
    private const string AppRequestName = nameof(UpdateAzureOpenAIConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateAzureOpenAIConnectionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(UpdateAzureOpenAIConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.AzureOpenAIConnections
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (connection is null)
            return Result.Failure<Guid>("Azure OpenAI connection not found.");

        var apiKey = ConnectionSecret.Replacement(request.ApiKey, connection.Configuration!.ApiKey);

        // TODO: Check the connection validity via some service making a "Test message" chat call using the connection creds
        var configurationIsValid = true;

        var updateResult = connection.Update(request.Name, request.Description, request.BaseUrl, apiKey, request.DeploymentName, configurationIsValid, EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
        if (updateResult.IsFailure)
        {
            _logger.LogInformation("Wayd Request: Failure for Request {Name}.  Error message: {Error}", AppRequestName, updateResult.Error);
            return Result.Failure<Guid>(updateResult.Error);
        }

        await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(connection.Id);
    }
}

