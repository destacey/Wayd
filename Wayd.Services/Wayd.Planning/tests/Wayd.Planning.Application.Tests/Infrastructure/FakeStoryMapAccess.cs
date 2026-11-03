using Wayd.Planning.Application.StoryMaps;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Infrastructure;

/// <summary>
/// An <see cref="IStoryMapAccess"/> that answers with whoever the test sets, so handler tests choose the caller
/// without the identity and organization lookups behind the real one. The map still decides.
/// </summary>
public sealed class FakeStoryMapAccess : IStoryMapAccess
{
    /// <summary>The caller commands act as; a generated or seeded map's owner unless set.</summary>
    public StoryMapActor Actor { get; set; } = StoryMapActors.Owner;

    /// <summary>The caller queries read as; the owner unless set.</summary>
    public StoryMapReader Reader { get; set; } =
        new(StoryMapActors.OwnerEmployeeId, false, [], false);

    /// <summary>What <see cref="CanUserAccess"/> answers.</summary>
    public bool UserAccess { get; set; } = true;

    /// <summary>How many times <see cref="ResolveActor"/> was asked, to show a retry resolves the caller again.</summary>
    public int ResolveActorCallCount { get; private set; }

    public Task<StoryMapActor> ResolveActor(StoryMap map, CancellationToken cancellationToken)
    {
        ResolveActorCallCount++;
        return Task.FromResult(Actor);
    }

    public Task<StoryMapReader> ResolveReader(CancellationToken cancellationToken) =>
        Task.FromResult(Reader);

    public Task<bool> CanUserAccess(Guid storyMapId, string userId, CancellationToken cancellationToken) =>
        Task.FromResult(UserAccess);
}
