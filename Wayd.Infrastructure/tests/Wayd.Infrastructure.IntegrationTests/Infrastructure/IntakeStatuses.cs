using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;

namespace Wayd.Infrastructure.IntegrationTests.Infrastructure;

/// <summary>
/// The statuses intake requests are created with in tests that save them.
/// </summary>
internal static class IntakeStatuses
{
    /// <summary>
    /// A Submitted status for a request created directly through the domain. A request's status is not a
    /// foreign key, so a test that is not about workflows needs no seeded one.
    /// </summary>
    public static StatusRef Submitted(Guid? workflowId = null) =>
        new(workflowId ?? Guid.CreateVersion7(), Guid.CreateVersion7(), "Submitted", StatusCategory.Proposed, (int)IntakeRequestStatusAlias.Submitted);

    /// <summary>
    /// A resolver over the seeded default intake workflow, seeding it first when the database has none, for
    /// tests running the commands that resolve a desk's workflow.
    /// </summary>
    public static async Task<IStatusResolver> SeededResolver(WaydDbContext context, CancellationToken cancellationToken)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        await new DemandManagementWorkflowSeeder().Initialize(context, clock.Object, cancellationToken);

        return new StatusResolver(context);
    }
}
