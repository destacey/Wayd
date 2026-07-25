using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddStepCommand(Guid StoryMapId, Guid GoalId, string Name) : ICommand<StoryMapStepDto>;

public sealed class AddStepCommandValidator : CustomValidator<AddStepCommand>
{
    public AddStepCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();

        RuleFor(c => c.GoalId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class AddStepCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<AddStepCommandHandler> logger) : ICommandHandler<AddStepCommand, StoryMapStepDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<AddStepCommandHandler> _logger = logger;

    public async Task<Result<StoryMapStepDto>> Handle(AddStepCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .Include(m => m.SwimLanes)
                .Include(m => m.Personas)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure<StoryMapStepDto>("Story map not found.");

            var result = map.AddStep(request.GoalId, request.Name);
            if (result.IsFailure)
                return Result.Failure<StoryMapStepDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var dto = result.Value.Adapt<StoryMapStepDto>();
            await _notifier.NotifyStepAdded(map.Id, dto);

            return Result.Success(dto);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<StoryMapStepDto>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
