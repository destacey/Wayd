using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Commands;

/// <summary>
/// Replace a story map's owners; at least one is required. Its owners and Story Maps administrators can, on an
/// active or an archived map.
/// </summary>
public sealed record SetStoryMapOwnersCommand(Guid Id, IReadOnlyCollection<Guid> EmployeeIds) : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects a command with no owners or an empty owner id.</summary>
public sealed class SetStoryMapOwnersCommandValidator : CustomValidator<SetStoryMapOwnersCommand>
{
    public SetStoryMapOwnersCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EmployeeIds)
            .NotEmpty()
            .WithMessage("A story map needs at least one owner.");
        RuleForEach(c => c.EmployeeIds).NotEmpty();
    }
}

/// <summary>
/// Replaces the owners and evicts live viewers the map no longer admits.
/// </summary>
/// <remarks>
/// Runs through <see cref="StoryMapMutation"/>, which writes the map's own row: owners live in their own table, so
/// without it two owners each removing the other would both save and leave the map with none.
/// </remarks>
public sealed class SetStoryMapOwnersCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<SetStoryMapOwnersCommandHandler> logger) : ICommandHandler<SetStoryMapOwnersCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<SetStoryMapOwnersCommandHandler> _logger = logger;

    /// <summary>Saves the new owners, refusing a caller who does not manage the map and owners who do not exist.</summary>
    public async Task<Result> Handle(SetStoryMapOwnersCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var exist = await _planningDbContext.EnsureEmployeesExist(request.EmployeeIds, cancellationToken);

            var result = await StoryMapMutation.Apply(
                _planningDbContext,
                _access,
                ct => _planningDbContext.StoryMaps
                    .IncludeAccess()
                    .FirstOrDefaultAsync(m => m.Id == request.Id, ct),
                // The manage check comes first, so a caller who cannot manage the map learns nothing about the employees.
                (map, actor) => !map.CanManage(actor) || exist.IsSuccess
                    ? map.SetOwners(request.EmployeeIds, actor)
                    : exist,
                cancellationToken);

            if (result.IsFailure)
                return result;

            await _notifier.NotifyAccessChanged(request.Id);

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
