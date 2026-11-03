using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record UpdateTaskCommand(Guid StoryMapId, Guid TaskId, string Title, string? Description) : ICommand, IRequireLinkedEmployee;

public sealed class UpdateTaskCommandValidator : CustomValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.TaskId).NotEmpty();

        RuleFor(c => c.Title)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(2048);
    }
}

public sealed class UpdateTaskCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<UpdateTaskCommandHandler> logger) : ICommandHandler<UpdateTaskCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<UpdateTaskCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .Include(m => m.Goals).ThenInclude(g => g.Steps).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var actor = await _access.ResolveActor(map, cancellationToken);

            var result = map.UpdateTask(request.TaskId, request.Title, request.Description, actor);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var task = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).First(t => t.Id == request.TaskId);
            var dto = task.Adapt<StoryMapTaskDto>();
            await _notifier.NotifyTaskUpdated(map.Id, dto);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
