using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Commands;

/// <summary>
/// Make a story map public or private. Its owners and Story Maps administrators can, on an active or an archived map.
/// </summary>
public sealed record SetStoryMapVisibilityCommand(Guid Id, Visibility Visibility) : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects an unknown visibility.</summary>
public sealed class SetStoryMapVisibilityCommandValidator : CustomValidator<SetStoryMapVisibilityCommand>
{
    public SetStoryMapVisibilityCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Visibility).IsInEnum();
    }
}

/// <summary>Changes who can see the map and evicts live viewers it no longer admits.</summary>
public sealed class SetStoryMapVisibilityCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<SetStoryMapVisibilityCommandHandler> logger) : ICommandHandler<SetStoryMapVisibilityCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<SetStoryMapVisibilityCommandHandler> _logger = logger;

    /// <summary>Saves the visibility, refusing a caller who does not manage the map.</summary>
    public async Task<Result> Handle(SetStoryMapVisibilityCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

            if (map is null)
                return Result.Failure(StoryMap.NotFoundError);

            var actor = await _access.ResolveActor(map, cancellationToken);

            var result = map.SetVisibility(request.Visibility, actor);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyAccessChanged(map.Id);

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
