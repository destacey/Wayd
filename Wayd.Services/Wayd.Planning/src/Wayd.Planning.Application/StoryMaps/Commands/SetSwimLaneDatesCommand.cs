using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record SetSwimLaneDatesCommand(Guid StoryMapId, Guid SwimLaneId, LocalDate? StartDate, LocalDate? EndDate) : ICommand;

public sealed class SetSwimLaneDatesCommandValidator : CustomValidator<SetSwimLaneDatesCommand>
{
    public SetSwimLaneDatesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.SwimLaneId).NotEmpty();
    }
}

public sealed class SetSwimLaneDatesCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<SetSwimLaneDatesCommandHandler> logger) : ICommandHandler<SetSwimLaneDatesCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<SetSwimLaneDatesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetSwimLaneDatesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.SwimLanes)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.SetSwimLaneDates(request.SwimLaneId, request.StartDate, request.EndDate);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var laneDto = map.SwimLanes.First(l => l.Id == request.SwimLaneId).Adapt<StoryMapSwimLaneDto>();
            await _notifier.NotifySwimLaneDatesChanged(map.Id, laneDto);

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
