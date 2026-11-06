using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the default workflow for project deliverables, assigned at the organization level so every project
/// follows it.
/// </summary>
public class PpmWorkflowSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        PpmWorkflowOwners.Register();

        var seeding = new SystemWorkflowSeeding();

        var seeded = await seeding.SeedIfAbsent(dbContext, PpmWorkflowOwners.ProjectDeliverable,
            "Default Deliverable Workflow",
            "A project deliverable, from commitment to acceptance.",
            [
                ("Not Started", "Committed to, with no work begun.", StatusCategory.Proposed, ProjectDeliverableStatusAlias.NotStarted),
                ("In Progress", "Being worked on.", StatusCategory.Active, ProjectDeliverableStatusAlias.InProgress),
                ("Accepted", "Delivered and accepted against its acceptance criteria.", StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted),
                ("Descoped", "Dropped from the project's scope.", StatusCategory.Removed, ProjectDeliverableStatusAlias.Descoped),
            ], dateTimeProvider, cancellationToken);

        if (seeded)
        {
            await seeding.Save(dbContext, cancellationToken);
        }
    }
}
