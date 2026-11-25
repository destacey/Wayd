using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.Persistence;

public interface IPlanningDbContext : IWaydDbContext
{
    DbSet<PlanningIntervalObjective> PlanningIntervalObjectives { get; }
    DbSet<PlanningInterval> PlanningIntervals { get; }
    DbSet<PlanningIntervalIterationSprint> PlanningIntervalIterationSprints { get; }
    DbSet<PlanningSprint> PlanningSprints { get; }
    DbSet<Risk> Risks { get; }
    DbSet<PlanningTeam> PlanningTeams { get; }
    DbSet<PlanningIntervalObjectiveHealthCheck> PlanningIntervalObjectiveHealthChecks { get; }
    DbSet<Roadmap> Roadmaps { get; }
    DbSet<EstimationScale> EstimationScales { get; }
    DbSet<EstimationScaleVersion> EstimationScaleVersions { get; }
    DbSet<PokerSession> PokerSessions { get; }
    DbSet<StoryMap> StoryMaps { get; }

    /// <summary>
    /// Begins a unit of work for adding or removing a participant's votes, holding a shared lock on the session's
    /// poker rounds — or on <paramref name="roundId"/> alone — until it ends. Votes in a round share the lock and do
    /// not wait on each other; an exclusive change to the round (<see cref="BeginExclusivePokerRoundChange"/>) waits
    /// for them to commit, and they wait for it. Load the session after this returns, so the load sees the round as
    /// the last such change left it, or finds it, or the session, gone.
    /// </summary>
    Task<IUnitOfWork> BeginPokerRoundVoting(Guid sessionId, Guid? roundId, CancellationToken cancellationToken);

    /// <summary>
    /// Begins a unit of work for a change that fixes, clears or deletes a poker round's votes — revealing, resetting
    /// or removing <paramref name="roundId"/>, or, with no round, completing or deleting the session — holding an
    /// exclusive lock on the round, or on every round in the session, until it ends, so no vote is added or withdrawn
    /// between the load and the commit. Load the session after this returns, so the load sees every vote that
    /// committed first.
    /// </summary>
    Task<IUnitOfWork> BeginExclusivePokerRoundChange(Guid sessionId, Guid? roundId, CancellationToken cancellationToken);
}
