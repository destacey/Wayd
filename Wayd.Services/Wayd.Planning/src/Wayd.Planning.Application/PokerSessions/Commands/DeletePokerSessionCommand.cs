namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record DeletePokerSessionCommand(Guid Id) : ICommand;

public sealed class DeletePokerSessionCommandValidator : CustomValidator<DeletePokerSessionCommand>
{
    public DeletePokerSessionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeletePokerSessionCommandHandler(IPlanningDbContext planningDbContext, ILogger<DeletePokerSessionCommandHandler> logger) : ICommandHandler<DeletePokerSessionCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<DeletePokerSessionCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeletePokerSessionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            _planningDbContext.PokerSessions.Remove(session);
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
