using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class WorkflowCheckRulesTests
{
    public static TheoryData<WorkflowCheck, WorkflowOwnership> EveryCheckAndOwnership()
    {
        var data = new TheoryData<WorkflowCheck, WorkflowOwnership>();
        foreach (var check in Enum.GetValues<WorkflowCheck>())
        {
            foreach (var ownership in Enum.GetValues<WorkflowOwnership>())
            {
                data.Add(check, ownership);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryCheckAndOwnership))]
    public void For_ShouldListEveryCheck_ForEveryOwnership(WorkflowCheck check, WorkflowOwnership ownership)
    {
        // Act
        var act = () => WorkflowCheckRules.For(check, ownership);

        // Assert
        act.Should().NotThrow("every engine check must say how it applies to owned and to managed workflows");
        Enum.IsDefined(act()).Should().BeTrue();
    }

    [Theory]
    [InlineData(WorkflowCheck.RequiredCategories, WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Warned)]
    [InlineData(WorkflowCheck.RequiredAliases, WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Warned)]
    [InlineData(WorkflowCheck.AliasCategories, WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Warned)]
    [InlineData(WorkflowCheck.Transitions, WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Skipped)]
    [InlineData(WorkflowCheck.Published, WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Enforced)]
    [InlineData(WorkflowCheck.CompleteRemap, WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Enforced)]
    public void For_ShouldGiveEachCheckItsSeverity(WorkflowCheck check, WorkflowCheckSeverity owned, WorkflowCheckSeverity managed)
    {
        // Act
        var forOwned = WorkflowCheckRules.For(check, WorkflowOwnership.Owned);
        var forManaged = WorkflowCheckRules.For(check, WorkflowOwnership.Managed);

        // Assert
        forOwned.Should().Be(owned);
        forManaged.Should().Be(managed);
    }

    [Fact]
    public void For_ShouldNeverRefuseAManagedWorkflowsStructure()
    {
        // Arrange — the checks a source's own workflow could fail, which the sync must follow regardless
        WorkflowCheck[] structural = [WorkflowCheck.RequiredCategories, WorkflowCheck.RequiredAliases, WorkflowCheck.AliasCategories, WorkflowCheck.Transitions];

        // Act
        var severities = structural.Select(c => WorkflowCheckRules.For(c, WorkflowOwnership.Managed));

        // Assert
        severities.Should().NotContain(WorkflowCheckSeverity.Enforced);
    }
}
