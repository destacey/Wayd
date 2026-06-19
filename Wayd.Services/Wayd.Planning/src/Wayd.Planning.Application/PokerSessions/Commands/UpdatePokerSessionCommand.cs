using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record UpdatePokerSessionCommand(Guid Id, string Name, int EstimationScaleId) : ICommand;

public sealed class UpdatePokerSessionCommandValidator : CustomValidator<UpdatePokerSessionCommand>
{
    public UpdatePokerSessionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(c => c.EstimationScaleId)
            .GreaterThan(0);
    }
}

internal sealed class UpdatePokerSessionCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionNotifier notifier, ILogger<UpdatePokerSessionCommandHandler> logger) : ICommandHandler<UpdatePokerSessionCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<UpdatePokerSessionCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdatePokerSessionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .Include(s => s.Rounds)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            if (request.EstimationScaleId != session.EstimationScaleId)
            {
                var scaleExists = await _planningDbContext.EstimationScales
                    .AnyAsync(s => s.Id == request.EstimationScaleId, cancellationToken);

                if (!scaleExists)
                    return Result.Failure("Estimation scale not found.");
            }

            var result = session.Update(request.Name, request.EstimationScaleId);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifySessionUpdated(session.Id);

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
