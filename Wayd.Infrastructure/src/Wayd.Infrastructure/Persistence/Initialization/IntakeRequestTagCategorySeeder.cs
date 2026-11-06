using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.Persistence.Activities;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the tag category an install starts with — Area, taking several tags and holding none — recording its
/// creation in the activity log.
/// </summary>
/// <remarks>
/// Enough to show the shape without handing a company someone else's taxonomy: the intake team adds the tags,
/// and renames, deactivates or deletes the category as it likes, which is why it is not a system category. Initial seed
/// only: once any category has been created — recorded in the activity log, which keeps it after the category is
/// deleted — this stays out of the way, so a company that deletes every category does not get Area back.
/// <para>
/// The creation event is written straight into the activity log rather than left for <c>SaveChanges</c> to
/// drain, because draining also publishes and Wolverine cannot route while seeders run.
/// </para>
/// </remarks>
public class IntakeRequestTagCategorySeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        if (await dbContext.IntakeRequestTagCategories.AnyAsync(cancellationToken)
            || await dbContext.ActivityLogs.AnyAsync(a => a.EventType == nameof(IntakeRequestTagCategoryCreatedEvent), cancellationToken))
            return;

        var area = IntakeRequestTagCategory.Create(
            "Area",
            "The part of the business or product a request concerns.",
            allowsMany: true,
            order: 1,
            EventActor.System,
            dateTimeProvider.Now);

        var ordinal = 0;
        foreach (var domainEvent in area.DomainEvents)
            dbContext.ActivityLogs.Add(ActivityLogEntryFactory.CreateActivityLogEntry(domainEvent, area, ordinal++, correlationId: null));

        area.ClearDomainEvents();
        dbContext.IntakeRequestTagCategories.Add(area);

        // One save, so the category is never committed without the entry recording its creation.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
