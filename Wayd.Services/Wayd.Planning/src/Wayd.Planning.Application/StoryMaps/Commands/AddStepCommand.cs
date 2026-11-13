using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddStepCommand(Guid StoryMapId, Guid GoalId, string Name) : ICommand<StoryMapStepDto>, IRequireLinkedEmployee;

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

public sealed class AddStepCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<AddStepCommandHandler> logger) : ICommandHandler<AddStepCommand, StoryMapStepDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<AddStepCommandHandler> _logger = logger;

    public async Task<Result<StoryMapStepDto>> Handle(AddStepCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .Include(m => m.Goals).ThenInclude(g => g.Steps)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure<StoryMapStepDto>("Story map not found.");

            var actor = await _access.ResolveActor(map, cancellationToken);

            var result = map.AddStep(request.GoalId, request.Name, actor);
            if (result.IsFailure)
                return Result.Failure<StoryMapStepDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var dto = result.Value.Adapt<StoryMapStepDto>();
            await _notifier.NotifyStepAdded(map.Id, dto);

            return Result.Success(dto);
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", requestName);
            return Result.Failure<StoryMapStepDto>($"Wayd Request: Exception for Request {requestName}");
        }
    }
}
