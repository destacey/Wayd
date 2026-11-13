using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record AddPokerRoundCommand(Guid SessionId, string? Label, string? Description = null) : ICommand<PokerRoundDto>, IRequireLinkedEmployee;

public sealed class AddPokerRoundCommandValidator : CustomValidator<AddPokerRoundCommand>
{
    public AddPokerRoundCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.Label).MaximumLength(512);
        RuleFor(c => c.Description).MaximumLength(PokerRound.DescriptionMaxLength);
    }
}

public sealed class AddPokerRoundCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier, ILogger<AddPokerRoundCommandHandler> logger) : ICommandHandler<AddPokerRoundCommand, PokerRoundDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<AddPokerRoundCommandHandler> _logger = logger;

    public async Task<Result<PokerRoundDto>> Handle(AddPokerRoundCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .IncludeAccess()
                .Include(s => s.Rounds)
                    .ThenInclude(r => r.Votes)
                        .ThenInclude(v => v.Participant)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            if (session is null)
                return Result.Failure<PokerRoundDto>("Poker session not found.");

            var actor = await _access.ResolveActor(session, cancellationToken);

            var result = session.AddRound(request.Label, request.Description, actor);
            if (result.IsFailure)
                return Result.Failure<PokerRoundDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var roundDto = result.Value.Adapt<PokerRoundDto>();
            await _notifier.NotifyRoundAdded(session.Id, roundDto);

            return roundDto;
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", requestName);
            return Result.Failure<PokerRoundDto>($"Wayd Request: Exception for Request {requestName}");
        }
    }
}
