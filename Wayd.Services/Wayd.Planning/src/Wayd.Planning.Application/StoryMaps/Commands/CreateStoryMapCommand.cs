using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Commands;

/// <summary>
/// Create a story map owned by the caller.
/// </summary>
/// <param name="Visibility">Public, which anyone with the Story Maps View permission can see, or Private.</param>
/// <param name="EditAccess">Who can change a public map's content.</param>
/// <param name="OwnerIds">Employees who own the map alongside the caller, who is always an owner.</param>
/// <param name="EditorIds">Employees invited to edit the map.</param>
/// <param name="EditorTeamIds">Teams whose members are invited to edit the map.</param>
public sealed record CreateStoryMapCommand(
    string Name,
    string? Description,
    Visibility Visibility = Visibility.Public,
    StoryMapEditAccess EditAccess = StoryMapEditAccess.AnyoneWhoCanView,
    IReadOnlyCollection<Guid>? OwnerIds = null,
    IReadOnlyCollection<Guid>? EditorIds = null,
    IReadOnlyCollection<Guid>? EditorTeamIds = null) : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;

/// <summary>Checks the name, description, visibility and edit access.</summary>
public sealed class CreateStoryMapCommandValidator : CustomValidator<CreateStoryMapCommand>
{
    public CreateStoryMapCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(2048);

        RuleFor(c => c.Visibility)
            .IsInEnum();

        RuleFor(c => c.EditAccess)
            .IsInEnum();
    }
}

/// <summary>Creates the map with the caller as its first owner, plus the owners and editors the command names.</summary>
public sealed class CreateStoryMapCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ILogger<CreateStoryMapCommandHandler> logger) : ICommandHandler<CreateStoryMapCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<CreateStoryMapCommandHandler> _logger = logger;

    /// <summary>Creates the map, refusing owners or editors who do not exist.</summary>
    /// <exception cref="ForbiddenException">Thrown via <c>LinkedEmployeeRequired</c> when the caller has no linked employee.</exception>
    public async Task<Result<ObjectIdAndKey>> Handle(CreateStoryMapCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            if (employeeId is null)
                LinkedEmployeeRequired.Throw();

            IReadOnlyCollection<Guid> ownerIds = [employeeId.Value, .. request.OwnerIds ?? []];
            var editorIds = request.EditorIds ?? [];
            var editorTeamIds = request.EditorTeamIds ?? [];

            var exist = Result.Combine(
                await _planningDbContext.EnsureEmployeesExist([.. ownerIds, .. editorIds], cancellationToken),
                await _planningDbContext.EnsureTeamsExist(editorTeamIds, cancellationToken));
            if (exist.IsFailure)
                return Result.Failure<ObjectIdAndKey>(exist.Error);

            var mapResult = StoryMap.Create(
                request.Name,
                request.Description,
                employeeId.Value,
                request.Visibility,
                request.EditAccess);

            if (mapResult.IsFailure)
                return Result.Failure<ObjectIdAndKey>(mapResult.Error);

            var map = mapResult.Value;

            // The creator owns the new map, which is all these two need.
            var creator = new StoryMapActor(employeeId.Value, false, new HashSet<Guid>());
            var setUp = Result.Combine(
                map.SetOwners(ownerIds, creator),
                map.SetEditors(editorIds, editorTeamIds, creator));
            if (setUp.IsFailure)
                return Result.Failure<ObjectIdAndKey>(setUp.Error);

            await _planningDbContext.StoryMaps.AddAsync(map, cancellationToken);
            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return new ObjectIdAndKey(map.Id, map.Key);
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<ObjectIdAndKey>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
