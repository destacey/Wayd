using FluentAssertions;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Models;
using Wayd.Tests.Shared;
using Wayd.Work.Domain.Models;
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

    #region SetExternalViewWorkItemUrlTemplate

    [Fact]
    public void SetExternalViewWorkItemUrlTemplate_TrimsTheTemplate()
    {
        // Arrange
        var workspace = new WorkspaceFaker().AsExternal().Generate();

        // Act
        var result = workspace.SetExternalViewWorkItemUrlTemplate("  https://dev.example/acme/_workitems/edit/  ", _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workspace.ExternalViewWorkItemUrlTemplate.Should().Be("https://dev.example/acme/_workitems/edit/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetExternalViewWorkItemUrlTemplate_ClearsTheTemplate_WhenNoneIsGiven(string? template)
    {
        // Arrange
        var workspace = new WorkspaceFaker()
            .AsExternal(externalViewWorkItemUrlTemplate: "https://dev.example/acme/_workitems/edit/")
            .Generate();

        // Act
        var result = workspace.SetExternalViewWorkItemUrlTemplate(template, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workspace.ExternalViewWorkItemUrlTemplate.Should().BeNull();
    }

    [Theory]
    [InlineData("javascript:alert(1)//")]
    [InlineData("dev.example/acme/_workitems/edit/")]
    public void SetExternalViewWorkItemUrlTemplate_WhenNotAWebAddress_ReturnsFailure(string template)
    {
        // Arrange
        var workspace = new WorkspaceFaker()
            .AsExternal(externalViewWorkItemUrlTemplate: "https://dev.example/acme/_workitems/edit/")
            .Generate();

        // Act
        var result = workspace.SetExternalViewWorkItemUrlTemplate(template, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        workspace.ExternalViewWorkItemUrlTemplate.Should().Be("https://dev.example/acme/_workitems/edit/");
    }

    [Fact]
    public void SetExternalViewWorkItemUrlTemplate_WhenAStoredTemplateIsUnchanged_Succeeds()
    {
        // Arrange — a template saved before templates were checked can still be saved again as it is
        var workspace = new WorkspaceFaker()
            .AsExternal(externalViewWorkItemUrlTemplate: "dev.example/acme/_workitems/edit/")
            .Generate();

        // Act
        var result = workspace.SetExternalViewWorkItemUrlTemplate("dev.example/acme/_workitems/edit/", _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CreateExternal_TrimsTheTemplate()
    {
        // Arrange
        var ownership = OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, "system-1", Guid.NewGuid().ToString());

        // Act
        var workspace = Workspace.CreateExternal(new WorkspaceKey("WEB"), "Web", null, ownership, Guid.NewGuid(), "  https://dev.example/acme/_workitems/edit/  ", _dateTimeProvider.Now);

        // Assert
        workspace.ExternalViewWorkItemUrlTemplate.Should().Be("https://dev.example/acme/_workitems/edit/");
    }

    [Fact]
    public void CreateExternal_WithATemplateThatIsNotAWebAddress_Throws()
    {
        // Arrange
        var ownership = OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, "system-1", Guid.NewGuid().ToString());

        // Act
        var act = () => Workspace.CreateExternal(new WorkspaceKey("WEB"), "Web", null, ownership, Guid.NewGuid(), "javascript:alert(1)//", _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("externalViewWorkItemUrlTemplate");
    }

    [Fact]
    public void SetExternalViewWorkItemUrlTemplate_WhenOwnedWorkspace_ReturnsFailure()
    {
        // Arrange
        var workspace = new WorkspaceFaker().Generate();

        // Act
        var result = workspace.SetExternalViewWorkItemUrlTemplate("https://dev.example/acme/_workitems/edit/", _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        workspace.ExternalViewWorkItemUrlTemplate.Should().BeNull();
    }

    #endregion

    #region WorkItemHistoryReadToEnd

    [Fact]
    public void WorkItemHistoryReadToEnd_WhenNotYetRead_RecordsTheInstant()
    {
        // Arrange
        var workspace = new WorkspaceFaker().AsExternal().Generate();

        // Act
        workspace.WorkItemHistoryReadToEnd(_dateTimeProvider.Now);

        // Assert
        workspace.WorkItemHistoryBackfilledOn.Should().Be(_dateTimeProvider.Now);
    }

    [Fact]
    public void WorkItemHistoryReadToEnd_WhenAlreadyRead_KeepsTheFirstInstant()
    {
        // Arrange
        var workspace = new WorkspaceFaker().AsExternal().Generate();
        var first = _dateTimeProvider.Now;
        workspace.WorkItemHistoryReadToEnd(first);

        // Act
        workspace.WorkItemHistoryReadToEnd(first.Plus(NodaTime.Duration.FromDays(1)));

        // Assert
        workspace.WorkItemHistoryBackfilledOn.Should().Be(first);
    }

    #endregion
}
