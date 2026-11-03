using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.Planning.Application.StoryMaps.Queries;

/// <summary>
/// The active direct members of a team invited to edit a story map, so the map can show who the invite reaches
/// without its reader needing the Teams permission.
/// </summary>
public sealed record GetStoryMapEditorTeamMembersQuery(Guid StoryMapId, Guid TeamId) : IQuery<IReadOnlyList<EmployeeNavigationDto>?>;

/// <summary>
/// Answers null when the caller cannot see the map or the team is not one of its editor teams, so the query cannot
/// list an arbitrary team.
/// </summary>
public sealed class GetStoryMapEditorTeamMembersQueryHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IDispatcher dispatcher)
    : IQueryHandler<GetStoryMapEditorTeamMembersQuery, IReadOnlyList<EmployeeNavigationDto>?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<IReadOnlyList<EmployeeNavigationDto>?> Handle(GetStoryMapEditorTeamMembersQuery request, CancellationToken cancellationToken)
    {
        var reader = await _access.ResolveReader(cancellationToken);

        var invited = await _planningDbContext.StoryMaps
            .Where(m => m.Id == request.StoryMapId)
            .Where(reader.Filter)
            .AnyAsync(m => m.EditorTeams.Any(t => t.TeamId == request.TeamId), cancellationToken);
        if (!invited)
            return null;

        return await _planningDbContext.ActiveTeamMembers(_dispatcher, request.TeamId, cancellationToken);
    }
}
