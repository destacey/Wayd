using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.Requests.WorkManagement;

public class SyncExternalWorkProcessWorkflowsCommandValidatorTests
{
    private readonly SyncExternalWorkProcessWorkflowsCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsAProcessWhoseWorkTypesWorkCanHold()
    {
        // Arrange
        var command = new SyncExternalWorkProcessWorkflowsCommand(Process(WorkType("Bug", null, "New")));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    /// <summary>
    /// The sync creates a work type Work does not have yet, so a name or description longer than its columns would
    /// fail at the database, part way through the sync, rather than here.
    /// </summary>
    [Theory]
    [InlineData(65, 0, 1, "WorkProcess.WorkTypes[0].Name")]
    [InlineData(5, 1025, 1, "WorkProcess.WorkTypes[0].Description")]
    [InlineData(5, 0, 65, "WorkProcess.WorkTypes[0].WorkflowStates[0].StatusName")]
    public void Validate_RejectsAWorkTypeWorkCannotHold(int nameLength, int descriptionLength, int stateNameLength, string propertyPath)
    {
        // Arrange
        var workType = WorkType(new string('n', nameLength), descriptionLength == 0 ? null : new string('d', descriptionLength), new string('s', stateNameLength));
        var command = new SyncExternalWorkProcessWorkflowsCommand(Process(workType));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Equal(propertyPath);
    }

    private static IExternalWorkProcessConfiguration Process(params IExternalWorkTypeWorkflow[] workTypes)
    {
        var process = new Mock<IExternalWorkProcessConfiguration>();
        process.Setup(p => p.Id).Returns(Guid.CreateVersion7());
        process.Setup(p => p.Name).Returns("Agile");
        process.Setup(p => p.WorkTypes).Returns(workTypes.ToList());
        return process.Object;
    }

    private static IExternalWorkTypeWorkflow WorkType(string name, string? description, string stateName)
    {
        var state = Mock.Of<IExternalWorkflowState>(s =>
            s.ExternalKey == "state-1" && s.StatusName == stateName && s.Category == StatusCategory.Proposed && s.Order == 1 && s.IsActive);
        var workType = new Mock<IExternalWorkTypeWorkflow>();
        workType.Setup(t => t.Name).Returns(name);
        workType.Setup(t => t.Description).Returns(description);
        workType.Setup(t => t.WorkTypeLevelId).Returns("level");
        workType.Setup(t => t.IsActive).Returns(true);
        workType.Setup(t => t.WorkflowStates).Returns([state]);
        return workType.Object;
    }
}
