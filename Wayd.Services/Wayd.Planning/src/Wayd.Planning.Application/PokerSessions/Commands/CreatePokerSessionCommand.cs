using Wayd.Common.Application.Models;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record CreatePokerSessionCommand(string Name, int EstimationScaleId) : ICommand<ObjectIdAndKey>;

public sealed class CreatePokerSessionCommandValidator : CustomValidator<CreatePokerSessionCommand>
{
    public CreatePokerSessionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(c => c.EstimationScaleId)
            .GreaterThan(0);
    }
}

public sealed class CreatePokerSessionCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<CreatePokerSessionCommandHandler> logger) : ICommandHandler<CreatePokerSessionCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreatePokerSessionCommandHandler> _logger = logger;
    private readonly string _currentUserId = currentUser.GetUserId();

    public async Task<Result<ObjectIdAndKey>> Handle(CreatePokerSessionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scaleExists = await _planningDbContext.EstimationScales
                .AnyAsync(s => s.Id == request.EstimationScaleId, cancellationToken);

            if (!scaleExists)
                return Result.Failure<ObjectIdAndKey>("Estimation scale not found.");

            var sessionResult = PokerSession.Create(
                request.Name,
                request.EstimationScaleId,
                _currentUserId,
                _dateTimeProvider.Now);

            if (sessionResult.IsFailure)
                return Result.Failure<ObjectIdAndKey>(sessionResult.Error);

            await _planningDbContext.PokerSessions.AddAsync(sessionResult.Value, cancellationToken);
            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return new ObjectIdAndKey(sessionResult.Value.Id, sessionResult.Value.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<ObjectIdAndKey>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
