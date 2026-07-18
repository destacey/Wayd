namespace Wayd.Common.Application.FeatureManagement.Commands;

public sealed record ArchiveFeatureFlagCommand(int Id) : ICommand;

public sealed class ArchiveFeatureFlagCommandValidator : CustomValidator<ArchiveFeatureFlagCommand>
{
    public ArchiveFeatureFlagCommandValidator()
    {
        RuleFor(c => c.Id).GreaterThan(0);
    }
}

public sealed class ArchiveFeatureFlagCommandHandler(
    IFeatureManagementDbContext dbContext,
    IFeatureFlagCacheInvalidator cacheInvalidator,
    ILogger<ArchiveFeatureFlagCommandHandler> logger) : ICommandHandler<ArchiveFeatureFlagCommand>
{
    private readonly IFeatureManagementDbContext _dbContext = dbContext;
    private readonly IFeatureFlagCacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly ILogger<ArchiveFeatureFlagCommandHandler> _logger = logger;

    public async Task<Result> Handle(ArchiveFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var featureFlag = await _dbContext.FeatureFlags
                .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

            if (featureFlag is null)
                return Result.Failure($"Feature flag with id {request.Id} not found.");

            var archiveResult = featureFlag.Archive();
            if (archiveResult.IsFailure)
                return archiveResult;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _cacheInvalidator.InvalidateCache();

            _logger.LogInformation("Feature flag archived: {FeatureFlagName} (Id: {FeatureFlagId})", featureFlag.Name, featureFlag.Id);

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
