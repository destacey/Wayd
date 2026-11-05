using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record AddSwimLaneCommand(Guid StoryMapId, string Name) : ICommand<StoryMapSwimLaneDto>, IRequireLinkedEmployee;

public sealed class AddSwimLaneCommandValidator : CustomValidator<AddSwimLaneCommand>
{
    public AddSwimLaneCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class AddSwimLaneCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<AddSwimLaneCommandHandler> logger) : ICommandHandler<AddSwimLaneCommand, StoryMapSwimLaneDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<AddSwimLaneCommandHandler> _logger = logger;

    public async Task<Result<StoryMapSwimLaneDto>> Handle(AddSwimLaneCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .Include(m => m.SwimLanes)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure<StoryMapSwimLaneDto>("Story map not found.");

            var actor = await _access.ResolveActor(map, cancellationToken);

            var result = map.AddSwimLane(request.Name, actor);
            if (result.IsFailure)
                return Result.Failure<StoryMapSwimLaneDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var dto = result.Value.Adapt<StoryMapSwimLaneDto>();
            await _notifier.NotifySwimLaneAdded(map.Id, dto);

            return Result.Success(dto);
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<StoryMapSwimLaneDto>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
