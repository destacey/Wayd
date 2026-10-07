using FluentAssertions;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.Workspaces.Queries;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.Workspaces.Queries;

public sealed class GetWorkItemHistoryWatermarkQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _fakeWorkDbContext = new();
    private readonly GetWorkItemHistoryWatermarkQueryHandler _handler;

    public GetWorkItemHistoryWatermarkQueryHandlerTests()
    {
        _handler = new GetWorkItemHistoryWatermarkQueryHandler(_fakeWorkDbContext);
    }

    public void Dispose()
    {
        _fakeWorkDbContext.Dispose();
    }

    [Theory]
    [InlineData("token-7", true)]
    [InlineData(null, false)]
    public async Task Handle_ReturnsTheWorkspacesWatermarkAndWhetherHistoryWasReadToTheEnd(string? watermark, bool readToEnd)
    {
        // Arrange
        var workspace = new WorkspaceFaker()
            .AsExternal()
            .WithWorkItemHistoryWatermark(watermark)
            .WithWorkItemHistoryBackfilledOn(readToEnd ? NodaTime.Instant.FromUtc(2026, 9, 1, 0, 0) : null)
            .Generate();
        _fakeWorkDbContext.AddWorkspace(workspace);

        // Act
        var result = await _handler.Handle(new GetWorkItemHistoryWatermarkQuery(workspace.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new WorkItemHistoryCursor(watermark, readToEnd));
    }

    [Fact]
    public async Task Handle_WhenTheWorkspaceIsWaydOwned_ReturnsFailure()
    {
        // Arrange — only a synced workspace has history to resume
        var workspace = new WorkspaceFaker().WithWorkItemHistoryWatermark("token-7").Generate();
        _fakeWorkDbContext.AddWorkspace(workspace);

        // Act
        var result = await _handler.Handle(new GetWorkItemHistoryWatermarkQuery(workspace.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheWorkspaceDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var query = new GetWorkItemHistoryWatermarkQuery(Guid.CreateVersion7());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
