using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class StatusRemapTests
{
    private const int NotableAlias = 11;
    private const int TerminalAlias = 12;

    private static readonly WorkflowOwnerDescriptor Widget = new(
        "test.remap.widget",
        "Widget",
        new Dictionary<int, string> { [NotableAlias] = "Notable", [TerminalAlias] = "Terminal" },
        [NotableAlias, TerminalAlias]);

    private static readonly WorkflowOwnerDescriptor Gadget = new(
        "test.remap.gadget",
        "Gadget",
        new Dictionary<int, string> { [NotableAlias] = "Notable" },
        [NotableAlias]);

    public StatusRemapTests() => WorkflowOwners.Register(Widget, Gadget);

    private static StatusWorkflow Workflow(
        string name,
        params (string Name, StatusCategory Category, int Alias)[] statuses)
    {
        var workflow = StatusWorkflow.Create(name, null, Widget.Key).Value;

        foreach (var (statusName, category, alias) in statuses)
        {
            workflow.AddStatus(statusName, null, category, alias);
        }

        return workflow;
    }

    #region Automatic mapping

    [Fact]
    public void AutoMap_ShouldMatchByAlias_WhateverTheStatusIsCalled()
    {
        // Arrange
        var from = Workflow("Old", ("Wrapped Up", StatusCategory.Done, TerminalAlias));
        var to = Workflow("New", ("Complete", StatusCategory.Done, TerminalAlias));

        // Act
        var remap = StatusRemap.AutoMap(from, to).Value;

        // Assert
        // The alias is unambiguous, which is why most of a remap needs no human: two organizations can
        // call the same meaning anything they like.
        var source = from.Statuses.Single();
        remap.For(source.Id)!.Name.Should().Be("Complete");
        remap.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void AutoMap_ShouldMatchByName_WhenNeitherCarriesAnAlias()
    {
        // Arrange
        var from = Workflow("Old",
            ("In Review", StatusCategory.Active, StatusWorkflow.NoAlias),
            ("Notable", StatusCategory.Active, NotableAlias),
            ("Terminal", StatusCategory.Done, TerminalAlias));
        var to = Workflow("New",
            ("in review", StatusCategory.Active, StatusWorkflow.NoAlias),
            ("Notable", StatusCategory.Active, NotableAlias),
            ("Terminal", StatusCategory.Done, TerminalAlias));

        // Act
        var remap = StatusRemap.AutoMap(from, to).Value;

        // Assert
        // Catches the statuses an organization invented and kept. Case-insensitive.
        var review = from.Statuses.Single(s => s.Name == "In Review");
        remap.For(review.Id)!.Name.Should().Be("in review");
    }

    [Fact]
    public void AutoMap_ShouldMatchByCategory_OnlyWhenThereIsNoChoice()
    {
        // Arrange
        var from = Workflow("Old",
            ("Blocked", StatusCategory.Removed, StatusWorkflow.NoAlias),
            ("Notable", StatusCategory.Active, NotableAlias),
            ("Terminal", StatusCategory.Done, TerminalAlias));
        var to = Workflow("New",
            ("Abandoned", StatusCategory.Removed, StatusWorkflow.NoAlias),
            ("Notable", StatusCategory.Active, NotableAlias),
            ("Terminal", StatusCategory.Done, TerminalAlias));

        // Act
        var remap = StatusRemap.AutoMap(from, to).Value;

        // Assert
        // One candidate in that category, so no decision is being made.
        var blocked = from.Statuses.Single(s => s.Name == "Blocked");
        remap.For(blocked.Id)!.Name.Should().Be("Abandoned");
    }

    [Fact]
    public void AutoMap_ShouldLeaveAStatusUnresolved_WhenSeveralCandidatesShareItsCategory()
    {
        // Arrange
        var from = Workflow("Old",
            ("Blocked", StatusCategory.Removed, StatusWorkflow.NoAlias),
            ("Notable", StatusCategory.Active, NotableAlias),
            ("Terminal", StatusCategory.Done, TerminalAlias));
        var to = Workflow("New",
            ("Abandoned", StatusCategory.Removed, StatusWorkflow.NoAlias),
            ("Rejected", StatusCategory.Removed, StatusWorkflow.NoAlias),
            ("Notable", StatusCategory.Active, NotableAlias),
            ("Terminal", StatusCategory.Done, TerminalAlias));

        // Act
        var remap = StatusRemap.AutoMap(from, to).Value;

        // Assert
        // Picking between Abandoned and Rejected would be a guess dressed as a decision.
        remap.IsComplete.Should().BeFalse();
        remap.Unresolved.Should().ContainSingle().Which.Name.Should().Be("Blocked");
    }

    [Fact]
    public void AutoMap_ShouldPreferAliasOverName()
    {
        // Arrange
        // "Terminal" exists in the target but carries no alias; the aliased status is called something
        // else entirely.
        var from = Workflow("Old", ("Terminal", StatusCategory.Done, TerminalAlias));
        var to = Workflow("New",
            ("Terminal", StatusCategory.Active, StatusWorkflow.NoAlias),
            ("Shipped", StatusCategory.Done, TerminalAlias));

        // Act
        var remap = StatusRemap.AutoMap(from, to).Value;

        // Assert
        // Meaning beats spelling: a same-named status with a different meaning is the wrong target.
        remap.For(from.Statuses.Single().Id)!.Name.Should().Be("Shipped");
    }

    [Fact]
    public void AutoMap_ShouldFail_AcrossOwnerTypes()
    {
        // Arrange
        var from = Workflow("Widget Workflow", ("Notable", StatusCategory.Active, NotableAlias));
        var gadget = StatusWorkflow.Create("Gadget Workflow", null, Gadget.Key).Value;

        // Act
        var result = StatusRemap.AutoMap(from, gadget);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be remapped");
    }

    #endregion Automatic mapping

    #region Resolving by hand

    [Fact]
    public void Resolve_ShouldCompleteARemapAHumanHasDecided()
    {
        // Arrange
        var from = Workflow("Old",
            ("Blocked", StatusCategory.Removed, StatusWorkflow.NoAlias),
            ("Notable", StatusCategory.Active, NotableAlias),
            ("Terminal", StatusCategory.Done, TerminalAlias));
        var to = Workflow("New",
            ("Abandoned", StatusCategory.Removed, StatusWorkflow.NoAlias),
            ("Rejected", StatusCategory.Removed, StatusWorkflow.NoAlias),
            ("Notable", StatusCategory.Active, NotableAlias),
            ("Terminal", StatusCategory.Done, TerminalAlias));

        var remap = StatusRemap.AutoMap(from, to).Value;
        var blocked = remap.Unresolved.Single();
        var rejected = to.Statuses.Single(s => s.Name == "Rejected");

        // Act
        var result = remap.Resolve(blocked.Id, rejected);

        // Assert
        result.IsSuccess.Should().BeTrue();
        remap.IsComplete.Should().BeTrue();
        remap.For(blocked.Id)!.Name.Should().Be("Rejected");
    }

    [Fact]
    public void Resolve_ShouldFail_WhenTheTargetIsFromAnotherWorkflow()
    {
        // Arrange
        var from = Workflow("Old", ("Notable", StatusCategory.Active, NotableAlias));
        var to = Workflow("New", ("Notable", StatusCategory.Active, NotableAlias));
        var elsewhere = Workflow("Unrelated", ("Notable", StatusCategory.Active, NotableAlias));

        var remap = StatusRemap.AutoMap(from, to).Value;

        // Act
        var result = remap.Resolve(from.Statuses.Single().Id, elsewhere.Statuses.Single());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A status can only be mapped to one in the workflow being moved to.");
    }

    [Fact]
    public void Resolve_ShouldOverrideAnAutomaticChoice()
    {
        // Arrange
        var from = Workflow("Old", ("Terminal", StatusCategory.Done, TerminalAlias));
        var to = Workflow("New",
            ("Terminal", StatusCategory.Done, TerminalAlias),
            ("Archived", StatusCategory.Done, StatusWorkflow.NoAlias));

        var remap = StatusRemap.AutoMap(from, to).Value;
        var source = from.Statuses.Single();
        remap.For(source.Id)!.Name.Should().Be("Terminal");

        // Act
        var result = remap.Resolve(source.Id, to.Statuses.Single(s => s.Name == "Archived"));

        // Assert
        // A person can disagree with the automatic choice.
        result.IsSuccess.Should().BeTrue();
        remap.For(source.Id)!.Name.Should().Be("Archived");
    }

    #endregion Resolving by hand
}
