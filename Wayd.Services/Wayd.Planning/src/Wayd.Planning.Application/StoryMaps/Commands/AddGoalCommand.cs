using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddGoalCommand(Guid StoryMapId, string Name) : ICommand<StoryMapGoalDto>, IRequireLinkedEmployee;

public sealed class AddGoalCommandValidator : CustomValidator<AddGoalCommand>
{
    public AddGoalCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class AddGoalCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<AddGoalCommandHandler> logger) : ICommandHandler<AddGoalCommand, StoryMapGoalDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<AddGoalCommandHandler> _logger = logger;

    public async Task<Result<StoryMapGoalDto>> Handle(AddGoalCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .Include(m => m.Goals)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure<StoryMapGoalDto>("Story map not found.");

            var actor = await _access.ResolveActor(map, cancellationToken);

            var result = map.AddGoal(request.Name, actor);
            if (result.IsFailure)
                return Result.Failure<StoryMapGoalDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var dto = result.Value.Adapt<StoryMapGoalDto>();
            await _notifier.NotifyGoalAdded(map.Id, dto);

            return Result.Success(dto);
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", requestName);
            return Result.Failure<StoryMapGoalDto>($"Wayd Request: Exception for Request {requestName}");
        }
    }
}
