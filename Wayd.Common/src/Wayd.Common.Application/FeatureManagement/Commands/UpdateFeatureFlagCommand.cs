namespace Wayd.Common.Application.FeatureManagement.Commands;

public sealed record UpdateFeatureFlagCommand(
    int Id,
    string DisplayName,
    string? Description
) : ICommand;

public sealed class UpdateFeatureFlagCommandValidator : CustomValidator<UpdateFeatureFlagCommand>
{
    public UpdateFeatureFlagCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id).GreaterThan(0);
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).MaximumLength(1024);
    }
}

public sealed class UpdateFeatureFlagCommandHandler(
    IFeatureManagementDbContext dbContext,
    ILogger<UpdateFeatureFlagCommandHandler> logger) : ICommandHandler<UpdateFeatureFlagCommand>
{
    private readonly IFeatureManagementDbContext _dbContext = dbContext;
    private readonly ILogger<UpdateFeatureFlagCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var featureFlag = await _dbContext.FeatureFlags
                .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

            if (featureFlag is null)
                return Result.Failure($"Feature flag with id {request.Id} not found.");

            var updateResult = featureFlag.Update(request.DisplayName, request.Description);
            if (updateResult.IsFailure)
                return updateResult;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Feature flag updated: {FeatureFlagName} (Id: {FeatureFlagId})", featureFlag.Name, featureFlag.Id);

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
