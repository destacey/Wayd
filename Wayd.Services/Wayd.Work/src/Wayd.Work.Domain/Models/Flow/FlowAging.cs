using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Domain.Models.Flow;

/// <summary>
/// Whether a Kanban team's work is getting stuck: how long its oldest item in progress has been there, against
/// how long the team usually takes to finish one.
/// </summary>
public static class FlowAging
{
    /// <summary>How many times the typical cycle time the oldest item may age before the team is watched.</summary>
    public const double WatchMultiple = 2;

    /// <summary>How many times the typical cycle time the oldest item may age before the team needs attention.</summary>
    public const double NeedsAttentionMultiple = 3;

    /// <summary>
    /// The least a typical cycle time is taken to be, in days, so a team that finishes most items within hours is
    /// not flagged for an item a day or two old.
    /// </summary>
    public const double MinimumCycleTimeDays = 1;

    /// <summary>
    /// Watch when the oldest item has been in progress more than <see cref="WatchMultiple"/> times the median
    /// cycle time, Needs Attention past <see cref="NeedsAttentionMultiple"/> times; On Track with nothing in
    /// progress or no recent median to compare with.
    /// </summary>
    /// <param name="oldestInProgressDays">How long the oldest item has been in progress; null with nothing in progress.</param>
    /// <param name="medianCycleTimeDays">The team's recent median cycle time; null when it finished nothing lately.</param>
    public static AttentionStatus Assess(double? oldestInProgressDays, double? medianCycleTimeDays)
    {
        if (oldestInProgressDays is not { } age || medianCycleTimeDays is not { } median)
            return AttentionStatus.OnTrack;

        var typical = Math.Max(median, MinimumCycleTimeDays);
        return age > typical * NeedsAttentionMultiple
            ? AttentionStatus.NeedsAttention
            : age > typical * WatchMultiple
                ? AttentionStatus.Watch
                : AttentionStatus.OnTrack;
    }
}
