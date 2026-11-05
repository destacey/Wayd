using FluentValidation.TestHelper;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

namespace Wayd.Web.Api.Tests.Sut.Models.DemandManagement.IntakeRequests;

public sealed class AddIntakeRequestOutcomeRequestValidatorTests
{
    private readonly AddIntakeRequestOutcomeRequestValidator _validator = new();

    [Theory]
    [InlineData(IntakeRequestOutcomeKind.NewProject)]
    [InlineData(IntakeRequestOutcomeKind.ExistingProjectScope)]
    public void ProjectKind_WithOnlyAProject_Passes(IntakeRequestOutcomeKind kind)
    {
        // Arrange
        var request = new AddIntakeRequestOutcomeRequest { Kind = kind, ProjectId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ProjectKind_WithoutAProject_FailsOnTheProject()
    {
        // Arrange
        var request = new AddIntakeRequestOutcomeRequest { Kind = IntakeRequestOutcomeKind.NewProject, WorkItemId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ProjectId);
        result.ShouldHaveValidationErrorFor(r => r.WorkItemId);
    }

    [Fact]
    public void WorkItemKind_WithOnlyAWorkItem_Passes()
    {
        // Arrange
        var request = new AddIntakeRequestOutcomeRequest { Kind = IntakeRequestOutcomeKind.WorkItem, WorkItemId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AnEmptyId_FailsOnThatId()
    {
        // Arrange
        var workItem = new AddIntakeRequestOutcomeRequest { Kind = IntakeRequestOutcomeKind.WorkItem, WorkItemId = Guid.Empty };
        var project = new AddIntakeRequestOutcomeRequest { Kind = IntakeRequestOutcomeKind.NewProject, ProjectId = Guid.Empty };

        // Act
        var workItemResult = _validator.TestValidate(workItem);
        var projectResult = _validator.TestValidate(project);

        // Assert
        workItemResult.ShouldHaveValidationErrorFor(r => r.WorkItemId);
        projectResult.ShouldHaveValidationErrorFor(r => r.ProjectId);
    }

    [Fact]
    public void WorkItemKind_GivenAProjectInstead_FailsOnBoth()
    {
        // Arrange
        var request = new AddIntakeRequestOutcomeRequest { Kind = IntakeRequestOutcomeKind.WorkItem, ProjectId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.WorkItemId);
        result.ShouldHaveValidationErrorFor(r => r.ProjectId);
    }
}
