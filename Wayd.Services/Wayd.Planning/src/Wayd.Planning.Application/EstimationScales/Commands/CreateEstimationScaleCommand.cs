using Wayd.Common.Application.Models;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.EstimationScales.Commands;

/// <summary>
/// Create an active estimation scale with <paramref name="Values"/> as its first version.
/// </summary>
public sealed record CreateEstimationScaleCommand(string Name, string? Description, List<EstimationScaleValueInput> Values) : ICommand<ObjectIdAndKey>;

public sealed class CreateEstimationScaleCommandValidator : CustomValidator<CreateEstimationScaleCommand>
{
    public CreateEstimationScaleCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.Values)
            .NotEmpty()
            .SetValidator(new EstimationScaleValuesValidator());
    }
}

public sealed class CreateEstimationScaleCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<CreateEstimationScaleCommandHandler> logger) : ICommandHandler<CreateEstimationScaleCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateEstimationScaleCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateEstimationScaleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scaleResult = EstimationScale.Create(
                request.Name,
                request.Description,
                request.Values.Select(v => v.ToValue()),
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                _dateTimeProvider.Now);

            if (scaleResult.IsFailure)
                return Result.Failure<ObjectIdAndKey>(scaleResult.Error);

            await _planningDbContext.EstimationScales.AddAsync(scaleResult.Value, cancellationToken);
            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return new ObjectIdAndKey(scaleResult.Value.Id, scaleResult.Value.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<ObjectIdAndKey>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
