using FluentAssertions;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.Workspaces.Queries;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.Workspaces.Queries;

public sealed class GetWorkspaceNeedsFullSyncQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _fakeWorkDbContext = new();
    private readonly GetWorkspaceNeedsFullSyncQueryHandler _handler;

    public GetWorkspaceNeedsFullSyncQueryHandlerTests()
    {
        _handler = new GetWorkspaceNeedsFullSyncQueryHandler(_fakeWorkDbContext);
    }

    public void Dispose()
    {
        _fakeWorkDbContext.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ReturnsWhetherTheWorkspaceNeedsAFullSync(bool needsFullSync)
    {
        // Arrange
        var workspace = new WorkspaceFaker().AsExternal().WithNeedsFullSync(needsFullSync).Generate();
        _fakeWorkDbContext.AddWorkspace(workspace);

        // Act
        var result = await _handler.Handle(new GetWorkspaceNeedsFullSyncQuery(workspace.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(needsFullSync);
    }

    [Fact]
    public async Task Handle_ForAWorkspaceThatIsNotManaged_Fails()
    {
        // Arrange
        var workspace = new WorkspaceFaker().WithNeedsFullSync(true).Generate();
        _fakeWorkDbContext.AddWorkspace(workspace);

        // Act
        var result = await _handler.Handle(new GetWorkspaceNeedsFullSyncQuery(workspace.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
