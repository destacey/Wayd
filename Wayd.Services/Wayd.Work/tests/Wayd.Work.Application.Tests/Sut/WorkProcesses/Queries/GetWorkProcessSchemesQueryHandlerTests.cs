using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProcesses.Queries;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProcesses.Queries;

public sealed class GetWorkProcessSchemesQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly GetWorkProcessSchemesQueryHandler _handler;

    public GetWorkProcessSchemesQueryHandlerTests()
    {
        MapsterTestConfiguration.Ensure();
        _handler = new GetWorkProcessSchemesQueryHandler(_dbContext, NullLogger<GetWorkProcessSchemesQueryHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoWorkProcessHasTheId()
    {
        // Arrange
        _dbContext.AddWorkProcess(new WorkProcessFaker().Generate());

        // Act
        var result = await _handler.Handle(new GetWorkProcessSchemesQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsAnEmptyList_WhenTheWorkProcessHasNoSchemes()
    {
        // Arrange
        var workProcess = new WorkProcessFaker().Generate();
        _dbContext.AddWorkProcess(workProcess);

        // Act
        var result = await _handler.Handle(new GetWorkProcessSchemesQuery(workProcess.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull().And.BeEmpty();
    }
}
