using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkTypes.Commands;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTypes.Commands;

public sealed class UpdateWorkTypeCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ShouldFail_WhenTheWorkTypeDoesNotExist()
    {
        // Arrange
        var sut = new UpdateWorkTypeCommandHandler(_dbContext, new TestingDateTimeProvider(new DateTime(2026, 1, 5, 12, 0, 0)), NullLogger<UpdateWorkTypeCommandHandler>.Instance);

        // Act
        var result = await sut.Handle(new UpdateWorkTypeCommand(404, "Description", 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Work Type not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
