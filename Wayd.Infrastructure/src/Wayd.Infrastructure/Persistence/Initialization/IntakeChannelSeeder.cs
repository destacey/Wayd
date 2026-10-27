using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.Persistence.Activities;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the intake channels an install starts with, recording each one's creation in the activity log.
/// </summary>
/// <remarks>
/// Initial seed only: once any channel exists this stays out of the way, so a company that archives or
/// renames the starting list keeps its own. No desk is seeded — a company creates the desks it will staff,
/// and one that runs no review desk has none.
/// <para>
/// The creation events are written straight into the activity log rather than left for <c>SaveChanges</c> to
/// drain, because draining also publishes and Wolverine cannot route while seeders run.
/// </para>
/// </remarks>
public class IntakeChannelSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        if (await dbContext.IntakeChannels.AnyAsync(cancellationToken))
            return;

        (string Name, string Description)[] seeds =
        [
            ("Business stakeholder", "Raised by someone in the business who needs something built or changed."),
            ("Customer feedback", "Something customers have asked for or told us."),
            ("Support", "Raised from support cases or what the support team keeps seeing."),
            ("Sales and account management", "Needed to win or keep a customer."),
            ("Innovation program", "An idea from an innovation program or hackathon."),
            ("Regulatory and compliance", "Required by a regulation, a standard or an audit."),
            ("Security", "Raised by security: a finding, a risk or a control."),
            ("Engineering", "Raised by engineering: technical debt, platform or tooling."),
        ];

        var now = dateTimeProvider.Now;
        var order = 1;
        var channels = seeds
            .Select(s => IntakeChannel.Create(s.Name, s.Description, order++, EventActor.System, now))
            .ToList();

        var ordinal = 0;
        foreach (var channel in channels)
        {
            foreach (var domainEvent in channel.DomainEvents)
                dbContext.ActivityLogs.Add(ActivityLogEntryFactory.CreateActivityLogEntry(domainEvent, channel, ordinal++, correlationId: null));

            channel.ClearDomainEvents();
            dbContext.IntakeChannels.Add(channel);
        }

        // One save, so a channel is never committed without the entry recording its creation.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
