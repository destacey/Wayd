namespace Wayd.Common.Application.FeatureManagement.Commands;

public sealed record ToggleFeatureFlagCommand(
    int Id,
    bool IsEnabled
) : ICommand;

public sealed class ToggleFeatureFlagCommandValidator : CustomValidator<ToggleFeatureFlagCommand>
{
    public ToggleFeatureFlagCommandValidator()
    {
        RuleFor(c => c.Id).GreaterThan(0);
    }
}

public sealed class ToggleFeatureFlagCommandHandler(
    IFeatureManagementDbContext dbContext,
    IFeatureFlagCacheInvalidator cacheInvalidator,
    ILogger<ToggleFeatureFlagCommandHandler> logger) : ICommandHandler<ToggleFeatureFlagCommand>
{
    private readonly IFeatureManagementDbContext _dbContext = dbContext;
    private readonly IFeatureFlagCacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly ILogger<ToggleFeatureFlagCommandHandler> _logger = logger;

    public async Task<Result> Handle(ToggleFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var featureFlag = await _dbContext.FeatureFlags
                .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

            if (featureFlag is null)
                return Result.Failure($"Feature flag with id {request.Id} not found.");

            featureFlag.Toggle(request.IsEnabled);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _cacheInvalidator.InvalidateCache();

            _logger.LogInformation("Feature flag toggled: {FeatureFlagName} (Id: {FeatureFlagId}) to {IsEnabled}",
                featureFlag.Name, featureFlag.Id, request.IsEnabled);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
