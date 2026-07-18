namespace Wayd.Planning.Application.EstimationScales.Commands;

public sealed record UpdateEstimationScaleCommand(int Id, string Name, string? Description, List<string> Values) : ICommand;

public sealed class UpdateEstimationScaleCommandValidator : CustomValidator<UpdateEstimationScaleCommand>
{
    public UpdateEstimationScaleCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id)
            .GreaterThan(0);

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.Values)
            .NotEmpty()
            .Must(v => v.Count >= 2).WithMessage("An estimation scale must have at least 2 values.");

        RuleForEach(c => c.Values)
            .NotEmpty()
            .MaximumLength(32);
    }
}

public sealed class UpdateEstimationScaleCommandHandler(IPlanningDbContext planningDbContext, ILogger<UpdateEstimationScaleCommandHandler> logger) : ICommandHandler<UpdateEstimationScaleCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<UpdateEstimationScaleCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateEstimationScaleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scale = await _planningDbContext.EstimationScales
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (scale is null)
                return Result.Failure("Estimation scale not found.");

            var updateResult = scale.Update(request.Name, request.Description);
            if (updateResult.IsFailure)
                return updateResult;

            var setValuesResult = scale.SetValues(request.Values);
            if (setValuesResult.IsFailure)
                return setValuesResult;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

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
