using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Commands;

/// <summary>
/// Choose who can change a public story map's content: anyone who can view it, or only its owners and editors. Its
/// owners and Story Maps administrators can, on an active or an archived map.
/// </summary>
public sealed record SetStoryMapEditAccessCommand(Guid Id, StoryMapEditAccess EditAccess) : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects an unknown edit access.</summary>
public sealed class SetStoryMapEditAccessCommandValidator : CustomValidator<SetStoryMapEditAccessCommand>
{
    public SetStoryMapEditAccessCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EditAccess).IsInEnum();
    }
}

/// <summary>Changes who can edit a public map and tells live viewers to refetch what they may do.</summary>
public sealed class SetStoryMapEditAccessCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<SetStoryMapEditAccessCommandHandler> logger) : ICommandHandler<SetStoryMapEditAccessCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<SetStoryMapEditAccessCommandHandler> _logger = logger;

    /// <summary>Saves the edit access, refusing a caller who does not manage the map.</summary>
    public async Task<Result> Handle(SetStoryMapEditAccessCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

            if (map is null)
                return Result.Failure(StoryMap.NotFoundError);

            var actor = await _access.ResolveActor(map, cancellationToken);

            var result = map.SetEditAccess(request.EditAccess, actor);
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
