using Microsoft.EntityFrameworkCore;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the intake channels an install starts with.
/// </summary>
/// <remarks>
/// Initial seed only: once any channel exists this stays out of the way, so a company that archives or
/// renames the starting list keeps its own. No desk is seeded — a company creates the desks it will staff,
/// and one that runs no review desk has none.
/// </remarks>
public class IntakeChannelSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        if (await dbContext.IntakeChannels.AnyAsync(cancellationToken))
            return;

        (string Name, string Description)[] channels =
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

        var order = 1;
        foreach (var (name, description) in channels)
        {
            dbContext.IntakeChannels.Add(IntakeChannel.Create(name, description, order++));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
