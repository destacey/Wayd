using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Persistence;

/// <summary>
/// The tables of the <c>Notifications</c> schema: who follows what, across every followable owner type.
/// </summary>
/// <remarks>
/// Separate from <see cref="IWaydDbContext"/> for the same reason as <see cref="IStatusWorkflowDbContext"/>:
/// the modules that make their records followable read it alongside their own context, and their test
/// fakes should not owe the whole global interface for one set.
/// </remarks>
public interface INotificationsDbContext
{
    /// <summary>
    /// Every follow. Reads are keyed by (<c>OwnerType</c>, <c>OwnerId</c>) or by <c>EmployeeId</c> —
    /// <c>OwnerId</c> alone is only unique within an owner type.
    /// </summary>
    /// <remarks>
    /// Read through this set; write through <c>IFollowStore</c>, which turns a concurrent duplicate follow
    /// into a no-op rather than a failed save.
    /// </remarks>
    DbSet<Follow> Follows { get; }
}
