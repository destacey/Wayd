using FluentAssertions;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProcesses.Queries;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProcesses.Queries;

public sealed class ExternalWorkProcessExistsQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly ExternalWorkProcessExistsQueryHandler _handler;

    public ExternalWorkProcessExistsQueryHandlerTests()
    {
        _handler = new ExternalWorkProcessExistsQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsTrue_WhenAWorkProcessHasTheExternalId()
    {
        // Arrange
        var externalProcessId = Guid.NewGuid();
        _dbContext.AddWorkProcess(new WorkProcessFaker().AsExternal(Guid.NewGuid()).Generate());
        _dbContext.AddWorkProcess(new WorkProcessFaker().AsExternal(externalProcessId).Generate());

        // Act
        var exists = await _handler.Handle(new ExternalWorkProcessExistsQuery(externalProcessId), TestContext.Current.CancellationToken);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNoWorkProcessHasTheExternalId()
    {
        // Arrange
        _dbContext.AddWorkProcess(new WorkProcessFaker().AsExternal(Guid.NewGuid()).Generate());
        _dbContext.AddWorkProcess(new WorkProcessFaker().WithExternalId(null).Generate());

        // Act
        var exists = await _handler.Handle(new ExternalWorkProcessExistsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        exists.Should().BeFalse();
    }
}
