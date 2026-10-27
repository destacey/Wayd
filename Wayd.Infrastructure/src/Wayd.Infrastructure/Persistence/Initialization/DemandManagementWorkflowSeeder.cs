using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the default review workflow for intake requests, assigned at the organization level so every desk
/// without a workflow of its own follows it.
/// </summary>
/// <remarks>
/// Seeded whether or not Demand Management is switched on, like the starting channels, so turning it on
/// needs nothing else. In Review and Declined carry no alias: In Review is the stage companies most often
/// split or rename, and Declined's rule (a reason) comes from the Removed category, so nothing resolves
/// either.
/// </remarks>
public class DemandManagementWorkflowSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        DemandManagementWorkflowOwners.Register();

        var seeding = new SystemWorkflowSeeding();

        var seeded = await seeding.SeedIfAbsent(dbContext, DemandManagementWorkflowOwners.IntakeRequest,
            "Default Intake Workflow",
            "The review of an intake request, from submission to a decision.",
            [
                ("Submitted", "Received, and waiting for the desk to look at it.", StatusCategory.Proposed, IntakeRequestStatusAlias.Submitted),
                ("In Review", "Being reviewed by the desk.", StatusCategory.Proposed, IntakeRequestStatusAlias.None),
                ("Waiting on Requester", "Waiting for the requester to answer a question.", StatusCategory.Proposed, IntakeRequestStatusAlias.WaitingOnRequester),
                ("Accepted", "Accepted for delivery.", StatusCategory.Active, IntakeRequestStatusAlias.Accepted),
                ("Fulfilled", "Delivered.", StatusCategory.Done, IntakeRequestStatusAlias.Fulfilled),
                ("Declined", "Not going ahead.", StatusCategory.Removed, IntakeRequestStatusAlias.None),
                ("Duplicate", "Already asked for in another request.", StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate),
                ("Withdrawn", "Withdrawn by the requester.", StatusCategory.Removed, IntakeRequestStatusAlias.Withdrawn),
            ], dateTimeProvider, cancellationToken);

        if (seeded)
        {
            await seeding.Save(dbContext, cancellationToken);
        }
    }
}
