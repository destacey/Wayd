using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Common.Application.FeatureManagement.Commands;

public sealed record CreateFeatureFlagCommand(
    string Name,
    string DisplayName,
    string? Description,
    bool IsEnabled
) : ICommand<int>;

public sealed class CreateFeatureFlagCommandValidator : CustomValidator<CreateFeatureFlagCommand>
{
    public CreateFeatureFlagCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).MaximumLength(1024);
    }
}

public sealed class CreateFeatureFlagCommandHandler(
    IFeatureManagementDbContext dbContext,
    IFeatureFlagCacheInvalidator cacheInvalidator,
    ILogger<CreateFeatureFlagCommandHandler> logger) : ICommandHandler<CreateFeatureFlagCommand, int>
{
    private readonly IFeatureManagementDbContext _dbContext = dbContext;
    private readonly IFeatureFlagCacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly ILogger<CreateFeatureFlagCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(CreateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existingFlag = await _dbContext.FeatureFlags
                .AnyAsync(f => f.Name.Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase), cancellationToken);

            if (existingFlag)
                return Result.Failure<int>($"A feature flag with the name '{request.Name}' already exists.");

            var createResult = FeatureFlag.Create(request.Name, request.DisplayName, request.Description, request.IsEnabled);
            if (createResult.IsFailure)
                return Result.Failure<int>(createResult.Error);

            await _dbContext.FeatureFlags.AddAsync(createResult.Value, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _cacheInvalidator.InvalidateCache();

            _logger.LogInformation("Feature flag created: {FeatureFlagName} (Id: {FeatureFlagId})", createResult.Value.Name, createResult.Value.Id);

            return Result.Success(createResult.Value.Id);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
