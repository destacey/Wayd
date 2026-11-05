using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps;

/// <summary>What the story map handlers share when loading a map.</summary>
public static class StoryMapQueryableExtensions
{
    /// <summary>
    /// Loads what <see cref="StoryMap.CanAccess"/>, <see cref="StoryMap.CanEdit"/> and <see cref="StoryMap.CanManage"/>
    /// read. Without it a map has no owners or editors, so a private map refuses everyone but administrators, and an
    /// owners-and-editors map lets no one but administrators change it; in-memory fakes cannot tell.
    /// </summary>
    public static IQueryable<StoryMap> IncludeAccess(this IQueryable<StoryMap> query) =>
        query
            .Include(m => m.Owners)
            .Include(m => m.Editors)
            .Include(m => m.EditorTeams);
}
