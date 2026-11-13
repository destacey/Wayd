using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Commands;

/// <summary>
/// Delete a story map and everything on it. Its owners and Story Maps administrators can.
/// </summary>
public sealed record DeleteStoryMapCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects an empty map id.</summary>
public sealed class DeleteStoryMapCommandValidator : CustomValidator<DeleteStoryMapCommand>
{
    public DeleteStoryMapCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

/// <summary>Soft-deletes the map and tells its live viewers it is gone.</summary>
public sealed class DeleteStoryMapCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<DeleteStoryMapCommandHandler> logger) : ICommandHandler<DeleteStoryMapCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<DeleteStoryMapCommandHandler> _logger = logger;

    /// <summary>Deletes the map, refusing a caller who does not manage it.</summary>
    public async Task<Result> Handle(DeleteStoryMapCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

            if (map is null)
                return Result.Failure(StoryMap.NotFoundError);

            var actor = await _access.ResolveActor(map, cancellationToken);

            var canDelete = map.CanDelete(actor);
            if (canDelete.IsFailure)
                return canDelete;

            // StoryMap is soft-deletable; the DbContext converts the Remove into a soft delete.
            _planningDbContext.StoryMaps.Remove(map);
            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyMapDeleted(request.Id);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", requestName);
            return Result.Failure($"Wayd Request: Exception for Request {requestName}");
        }
    }
}
