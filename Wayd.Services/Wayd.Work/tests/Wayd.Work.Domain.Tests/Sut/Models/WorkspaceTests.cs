using FluentAssertions;
using Wayd.Tests.Shared;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class WorkspaceTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;

    public WorkspaceTests()
    {
        _dateTimeProvider = new(new DateTime(2026, 04, 12, 12, 0, 0));
    }

    #region ChangeWorkProcess

    [Fact]
    public void ChangeWorkProcess_WhenManagedWorkspace_Succeeds()
    {
        // Arrange
        var oldProcessId = Guid.CreateVersion7();
        var newProcessId = Guid.CreateVersion7();
        var workspace = new WorkspaceFaker()
            .AsExternal()
            .WithWorkProcessId(oldProcessId)
            .Generate();

        // Act
        var result = workspace.ChangeWorkProcess(newProcessId, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workspace.WorkProcessId.Should().Be(newProcessId);
    }

    [Fact]
    public void ChangeWorkProcess_WhenSameProcess_ReturnsSuccessWithNoChange()
    {
        // Arrange
        var processId = Guid.CreateVersion7();
        var workspace = new WorkspaceFaker()
            .AsExternal()
            .WithWorkProcessId(processId)
            .Generate();

        // Act
        var result = workspace.ChangeWorkProcess(processId, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workspace.WorkProcessId.Should().Be(processId);
    }

    [Fact]
    public void ChangeWorkProcess_WhenOwnedWorkspace_ReturnsFailure()
    {
        // Arrange - default WorkspaceFaker creates an owned workspace
        var workspace = new WorkspaceFaker().Generate();

        // Act
        var result = workspace.ChangeWorkProcess(Guid.CreateVersion7(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("non-managed");
    }

    #endregion
}
