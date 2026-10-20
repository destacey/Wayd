using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Queries;

/// <summary>
/// Previewing a reassignment: the automatic mapping, and how many records stand behind each status.
/// </summary>
public sealed class PreviewStatusRemapQueryHandlerTests : StatusWorkflowHandlerTestBase
{
    private readonly Mock<IStatusRecordCounter> _counter = new();

    public PreviewStatusRemapQueryHandlerTests()
    {
        _counter.SetupGet(c => c.OwnerType).Returns(Widget.Key);
    }

    private PreviewStatusRemapQueryHandler CreateSut() => new(DbContext, [_counter.Object]);

    private (WorkflowAssignment Assignment, StatusWorkflow Current, StatusWorkflow Target) SeedReassignment(Guid? scopeId = null)
    {
        var current = SeedWorkflow("Current", publish: true);
        var target = SeedWorkflow("Target", publish: true);

        var assignment = WorkflowAssignment.Create(Widget.Key, scopeId, current, EventActor.System, Now).Value;
        assignment.ClearDomainEvents();
        DbContext.AddWorkflowAssignment(assignment);

        return (assignment, current, target);
    }

    [Fact]
    public async Task Handle_ShouldCountTheRecordsBehindEachStatus()
    {
        // Arrange
        var (assignment, current, target) = SeedReassignment();
        var notable = current.Statuses.Single(s => s.Alias == NotableAlias);

        _counter
            .Setup(c => c.CountByStatus(current.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int> { [notable.Id] = 4 }));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PreviewStatusRemapQuery(assignment.Id, target.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AffectedRecordCount.Should().Be(4);
        result.Value.Entries.Single(e => e.From.Id == notable.Id).RecordCount.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheRecordsCannotBeCounted()
    {
        // Arrange
        var scopeId = Guid.CreateVersion7();
        var (assignment, current, target) = SeedReassignment(scopeId);

        _counter
            .Setup(c => c.CountByStatus(current.Id, scopeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IReadOnlyDictionary<Guid, int>>("Records of this type have no scope."));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PreviewStatusRemapQuery(assignment.Id, target.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Records of this type have no scope.");
    }
}
