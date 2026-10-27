using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.Persistence.Activities;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the tag axis an install starts with — Area, taking several tags and holding none — recording its
/// creation in the activity log.
/// </summary>
/// <remarks>
/// Enough to show the shape without handing a company someone else's taxonomy: the intake team adds the tags,
/// and renames, deactivates or deletes the axis as it likes, which is why it is not a system axis. Initial seed
/// only: once any axis exists this stays out of the way, so a deleted Area does not come back.
/// <para>
/// The creation event is written straight into the activity log rather than left for <c>SaveChanges</c> to
/// drain, because draining also publishes and Wolverine cannot route while seeders run.
/// </para>
/// </remarks>
public class IntakeRequestTagCategorySeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        if (await dbContext.IntakeRequestTagCategories.AnyAsync(cancellationToken))
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

        // One save, so the axis is never committed without the entry recording its creation.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
