using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Commands;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Commands;

/// <summary>
/// Only the failure path: the delete is set-based, which the fake context cannot run. The reset
/// itself is covered against SQL Server in <c>SyncExternalWorkItemHistoryCommandHandlerTests</c>.
/// </summary>
public sealed class ResetWorkItemHistoryCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _fakeWorkDbContext = new();
    private readonly ResetWorkItemHistoryCommandHandler _handler;

    public ResetWorkItemHistoryCommandHandlerTests()
    {
        _handler = new ResetWorkItemHistoryCommandHandler(_fakeWorkDbContext, Mock.Of<ILogger<ResetWorkItemHistoryCommandHandler>>());
    }

    public void Dispose()
    {
        _fakeWorkDbContext.Dispose();
    }

    [Fact]
    public async Task Handle_WhenTheWorkspaceDoesNotExist_ReturnsFailureAndOpensNoTransaction()
    {
        // Arrange
        var command = new ResetWorkItemHistoryCommand(Guid.CreateVersion7());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _fakeWorkDbContext.UnitOfWorkCommitCount.Should().Be(0);
        _fakeWorkDbContext.SaveChangesCallCount.Should().Be(0);
    }
}
