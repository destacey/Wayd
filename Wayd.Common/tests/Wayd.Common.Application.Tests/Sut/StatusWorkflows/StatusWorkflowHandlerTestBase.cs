using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows;

/// <summary>
/// Shared setup for the status workflow handlers.
/// </summary>
/// <remarks>
/// The owner type is fictional on purpose. The engine has to work for any module that registers a
/// descriptor, and testing it against Product Management's vocabulary would let a coupling back to that
/// module pass unnoticed.
/// </remarks>
public abstract class StatusWorkflowHandlerTestBase
{
    protected const int NotableAlias = 11;
    protected const int TerminalAlias = 12;

    protected static readonly Instant Now = Instant.FromUtc(2026, 5, 1, 9, 0, 0);

    protected static readonly WorkflowOwnerDescriptor Widget = new(
        "test.widget",
        "Widget",
        new Dictionary<int, string> { [NotableAlias] = "Notable", [TerminalAlias] = "Terminal" },
        [NotableAlias, TerminalAlias]);

    protected readonly FakeWaydDbContext DbContext = new();
    protected readonly Mock<ICurrentUser> CurrentUser = new();
    protected readonly Mock<IDateTimeProvider> DateTimeProvider = new();

    protected StatusWorkflowHandlerTestBase()
    {
        WorkflowOwners.Register(Widget);
        CurrentUser.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());
        DateTimeProvider.SetupGet(d => d.Now).Returns(Now);
    }

    protected static ILogger<T> Logger<T>() => NullLogger<T>.Instance;

    /// <summary>A draft workflow carrying both required aliases.</summary>
    protected StatusWorkflow SeedWorkflow(string name = "Widget Workflow", bool publish = false)
    {
        var workflow = StatusWorkflow.Create(name, null, Widget.Key).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed);
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias);
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias);

        if (publish)
        {
            workflow.Publish(EventActor.System, Now);
            workflow.ClearDomainEvents();
        }

        DbContext.AddStatusWorkflow(workflow);

        return workflow;
    }
}
