using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Commands;

/// <summary>
/// Replace the employees and teams invited to edit a story map. Its owners and Story Maps administrators can, on an
/// active or an archived map.
/// </summary>
public sealed record SetStoryMapEditorsCommand(Guid Id, IReadOnlyCollection<Guid> EmployeeIds, IReadOnlyCollection<Guid> TeamIds) : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects missing lists and empty ids; empty lists are allowed and remove every editor.</summary>
public sealed class SetStoryMapEditorsCommandValidator : CustomValidator<SetStoryMapEditorsCommand>
{
    public SetStoryMapEditorsCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EmployeeIds).NotNull();
        RuleFor(c => c.TeamIds).NotNull();
        RuleForEach(c => c.EmployeeIds).NotEmpty();
        RuleForEach(c => c.TeamIds).NotEmpty();
    }
}

/// <summary>
/// Replaces the editors and evicts live viewers the map no longer admits.
/// </summary>
/// <remarks>
/// Runs through <see cref="StoryMapMutation"/>, which writes the map's own row: editors live in their own tables, so
/// without it two concurrent changes would both save, each undoing part of the other unnoticed.
/// </remarks>
public sealed class SetStoryMapEditorsCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<SetStoryMapEditorsCommandHandler> logger) : ICommandHandler<SetStoryMapEditorsCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<SetStoryMapEditorsCommandHandler> _logger = logger;

    /// <summary>Saves the new editors, refusing a caller who does not manage the map and people or teams who do not exist.</summary>
    public async Task<Result> Handle(SetStoryMapEditorsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var exist = Result.Combine(
                await _planningDbContext.EnsureEmployeesExist(request.EmployeeIds, cancellationToken),
                await _planningDbContext.EnsureTeamsExist(request.TeamIds, cancellationToken));

            var result = await StoryMapMutation.Apply(
                _planningDbContext,
                _access,
                ct => _planningDbContext.StoryMaps
                    .IncludeAccess()
                    .FirstOrDefaultAsync(m => m.Id == request.Id, ct),
                // The manage check comes first, so a caller who cannot manage the map learns nothing about the editors.
                (map, actor) => !map.CanManage(actor) || exist.IsSuccess
                    ? map.SetEditors(request.EmployeeIds, request.TeamIds, actor)
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
