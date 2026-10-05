using Wayd.Common.Application.Interfaces.ExternalWork;

namespace Wayd.Integrations.Abstractions;

/// <summary>
/// One batch of work item revisions returned by <see cref="IWorkItemSource.GetWorkItemHistory"/>.
/// </summary>
/// <param name="Revisions">The revisions in this batch, in the order the source returned them.</param>
/// <param name="NextWatermark">
/// The opaque token to pass on the next call, to continue after this batch. Wayd stores it with the
/// batch it covers and never interprets it.
/// </param>
/// <param name="IsLastBatch">Whether the source has nothing more to return for now.</param>
public sealed record WorkItemHistoryBatch(
    IReadOnlyList<IExternalWorkItemRevision> Revisions,
    string? NextWatermark,
    bool IsLastBatch)
{
    /// <summary>An empty, final batch that leaves the watermark at <paramref name="watermark"/>.</summary>
    public static WorkItemHistoryBatch Empty(string? watermark) => new([], watermark, true);
}
