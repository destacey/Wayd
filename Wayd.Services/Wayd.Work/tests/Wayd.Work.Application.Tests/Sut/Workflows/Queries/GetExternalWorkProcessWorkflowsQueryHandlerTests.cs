using FluentAssertions;
using Wayd.Common.Application.Requests.WorkManagement.Dtos;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Tests.Shared.Extensions;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.Workflows.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.Workflows.Queries;

public sealed class GetExternalWorkProcessWorkflowsQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly GetExternalWorkProcessWorkflowsQueryHandler _handler;

    public GetExternalWorkProcessWorkflowsQueryHandlerTests()
    {
        _handler = new GetExternalWorkProcessWorkflowsQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoWorkProcessHasTheId()
    {
        // Arrange
        _dbContext.AddWorkProcess(new WorkProcessFaker().Generate());

        // Act
        var result = await _handler.Handle(new GetExternalWorkProcessWorkflowsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsTheWorkflowOfEachWorkType_ByWorkTypeName()
    {
        // Arrange
        var bugWorkflow = new WorkflowFaker().Generate();
        var storyWorkflow = new WorkflowFaker().Generate();
        var noWorkflow = new WorkProcessSchemeFaker().WithWorkType(new WorkTypeFaker().WithName("Epic").Generate()).Generate();
        noWorkflow.SetPrivate(s => s.WorkflowId, null);
        noWorkflow.SetPrivate(s => s.Workflow, null);
        var process = new WorkProcessFaker().WithSchemes(
        [
            new WorkProcessSchemeFaker().WithWorkType(new WorkTypeFaker().WithName("User Story").Generate()).WithWorkflow(storyWorkflow).Generate(),
            noWorkflow,
            new WorkProcessSchemeFaker().WithWorkType(new WorkTypeFaker().WithName("Bug").Generate()).WithWorkflow(bugWorkflow).Generate(),
        ]).Generate();
        _dbContext.AddWorkProcess(process);

        // Act
        var result = await _handler.Handle(new GetExternalWorkProcessWorkflowsQuery(process.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Equal(
            new ExternalWorkProcessWorkflowDto("Bug", bugWorkflow.Id, bugWorkflow.Name, bugWorkflow.Description),
            new ExternalWorkProcessWorkflowDto("Epic", null, null, null),
            new ExternalWorkProcessWorkflowDto("User Story", storyWorkflow.Id, storyWorkflow.Name, storyWorkflow.Description));
    }
}
