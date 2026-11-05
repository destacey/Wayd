using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Queries;

/// <summary>
/// The owner types a workflow can be built for.
/// </summary>
public sealed class GetWorkflowOwnerTypesQueryHandlerTests : StatusWorkflowHandlerTestBase
{
    // An owner type that names one category its own way. Static, so registering it again is a no-op.
    private static readonly WorkflowOwnerDescriptor Labelled = new(
        "test.owner-types-labelled", "Labelled", new Dictionary<int, WorkflowAlias>(), [],
        categoryLabels: new Dictionary<StatusCategory, string> { [StatusCategory.Removed] = "Shelved" });

    [Fact]
    public async Task Handle_ShouldLeaveOutAnOwnerTypeWhoseFeatureIsOff()
    {
        // Arrange
        var sut = new GetWorkflowOwnerTypesQueryHandler(OwnerAvailability);

        // Act
        var owners = await sut.Handle(new GetWorkflowOwnerTypesQuery(), TestContext.Current.CancellationToken);

        // Assert
        owners.Select(o => o.Key).Should().Contain(Widget.Key).And.NotContain(Gadget.Key);
    }

    [Fact]
    public async Task Handle_ShouldDescribeEachAliasWithItsCategory_AndTheRequiredCategories()
    {
        // Arrange
        var sut = new GetWorkflowOwnerTypesQueryHandler(OwnerAvailability);

        // Act
        var owners = await sut.Handle(new GetWorkflowOwnerTypesQuery(), TestContext.Current.CancellationToken);

        // Assert
        var widget = owners.Single(o => o.Key == Widget.Key);
        widget.Aliases.Select(a => (a.Name, a.Category)).Should().Equal(
            ("Notable", StatusCategory.Active), ("Terminal", StatusCategory.Done));
        widget.RequiredCategories.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldLabelEveryCategory_WithTheOwnerTypesWordsOrTheCategoryName()
    {
        // Arrange
        WorkflowOwners.Register(Labelled);
        var sut = new GetWorkflowOwnerTypesQueryHandler(OwnerAvailability);

        // Act
        var owners = await sut.Handle(new GetWorkflowOwnerTypesQuery(), TestContext.Current.CancellationToken);

        // Assert
        owners.Single(o => o.Key == Labelled.Key).CategoryLabels.Select(c => (c.Category, c.Label)).Should().Equal(
            (StatusCategory.Proposed, "Proposed"),
            (StatusCategory.Active, "Active"),
            (StatusCategory.Done, "Done"),
            (StatusCategory.Removed, "Shelved"));
        owners.Single(o => o.Key == Widget.Key).CategoryLabels.Select(c => c.Label).Should().Equal(
            "Proposed", "Active", "Done", "Removed");
    }

    [Fact]
    public async Task Handle_ShouldIncludeTheOwnerType_OnceItsFeatureIsOn()
    {
        // Arrange
        EnableGadgets();
        var sut = new GetWorkflowOwnerTypesQueryHandler(OwnerAvailability);

        // Act
        var owners = await sut.Handle(new GetWorkflowOwnerTypesQuery(), TestContext.Current.CancellationToken);

        // Assert
        owners.Select(o => o.Key).Should().Contain(Gadget.Key);
    }
}
