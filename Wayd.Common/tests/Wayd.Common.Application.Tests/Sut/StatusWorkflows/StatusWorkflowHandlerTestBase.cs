using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
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
        [NotableAlias, TerminalAlias],
        supportsRestrictedTransitions: true);

    /// <summary>The feature flag <see cref="Gadget"/> is released behind. Off unless a test turns it on.</summary>
    protected const string GadgetFeature = "test-gadgets";

    /// <summary>An owner type released behind a feature flag, so workflow administration hides it while the flag is off.</summary>
    protected static readonly WorkflowOwnerDescriptor Gadget = new(
        "test.flagged-gadget",
        "Gadget",
        new Dictionary<int, string> { [NotableAlias] = "Notable", [TerminalAlias] = "Terminal" },
        [NotableAlias, TerminalAlias],
        featureFlag: GadgetFeature);

    protected readonly FakeWaydDbContext DbContext = new();
    protected readonly Mock<ICurrentUser> CurrentUser = new();
    protected readonly Mock<IDateTimeProvider> DateTimeProvider = new();
    protected readonly Mock<IFeatureFlags> FeatureFlags = new();
    protected readonly IWorkflowOwnerAvailability OwnerAvailability;

    protected StatusWorkflowHandlerTestBase()
    {
        WorkflowOwners.Register(Widget, Gadget);
        CurrentUser.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());
        DateTimeProvider.SetupGet(d => d.Now).Returns(Now);
        OwnerAvailability = new WorkflowOwnerAvailability(FeatureFlags.Object);
    }

    /// <summary>Turns on the feature flag <see cref="Gadget"/> is released behind.</summary>
    protected void EnableGadgets() =>
        FeatureFlags.Setup(f => f.IsEnabled(GadgetFeature, It.IsAny<CancellationToken>())).ReturnsAsync(true);

    protected static ILogger<T> Logger<T>() => NullLogger<T>.Instance;

    /// <summary>A draft workflow carrying both required aliases.</summary>
    protected StatusWorkflow SeedWorkflow(string name = "Widget Workflow", bool publish = false, WorkflowOwnerDescriptor? owner = null)
    {
        var workflow = StatusWorkflow.Create(name, null, (owner ?? Widget).Key, EventActor.System, Now).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Now);
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, Now);

        if (publish)
        {
            workflow.Publish(EventActor.System, Now);
            workflow.ClearDomainEvents();
        }

        DbContext.AddStatusWorkflow(workflow);

        return workflow;
    }
}
