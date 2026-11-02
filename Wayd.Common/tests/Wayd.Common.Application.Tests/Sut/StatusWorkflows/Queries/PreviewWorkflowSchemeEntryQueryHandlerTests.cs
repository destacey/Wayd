using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Queries;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Queries;

/// <summary>Previewing a change to one record type's entry.</summary>
public sealed class PreviewWorkflowSchemeEntryQueryHandlerTests : WorkflowSchemeHandlerTestBase
{
    private PreviewWorkflowSchemeEntryQueryHandler CreateSut(params IRecordTypeStatusMigrator[] migrators) =>
        new(DbContext, migrators, OwnerAvailability);

    [Fact]
    public async Task Handle_ShouldCountTheTypesRecordsPerStatus_InEveryScopeOnTheScheme()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var scheme = Scheme(standard);
        var scopeId = Guid.NewGuid();
        Assign(scheme, standard, scopeId);
        var notable = standard.Statuses.Single(s => s.Name == "Notable").Id;
        RecordSelection? counted = null;
        Migrator
            .Setup(m => m.CountByStatus(standard.Id, It.IsAny<RecordSelection>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, RecordSelection, CancellationToken>((_, selection, _) => counted = selection)
            .ReturnsAsync(Result.Success<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int> { [notable] = 4 }));

        // Act
        var result = await CreateSut(Migrator.Object).Handle(
            new PreviewWorkflowSchemeEntryQuery(scheme.Id, Bug.Id, bugFlow.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.AssignedScopeCount.Should().Be(1);
        result.Value.CanMoveRecords.Should().BeTrue();
        result.Value.AffectedRecordCount.Should().Be(4);
        var move = result.Value.Moves.Should().ContainSingle().Subject;
        move.RecordType!.Id.Should().Be(Bug.Id);
        move.Remap.To.Id.Should().Be(bugFlow.Id);
        move.Remap.Entries.Single(e => e.From.Id == notable).RecordCount.Should().Be(4);
        counted!.RecordTypeId.Should().Be(Bug.Id);
        counted.ScopeIds.Should().BeEquivalentTo(new Guid?[] { scopeId });
    }

    [Fact]
    public async Task Handle_ShouldSayTheChangeCannotBeApplied_WhenTheSchemeIsInUseAndNothingCanMoveItsRecords()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var scheme = Scheme(standard);
        Assign(scheme, standard, null);

        // Act
        var result = await CreateSut().Handle(
            new PreviewWorkflowSchemeEntryQuery(scheme.Id, Bug.Id, bugFlow.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CanMoveRecords.Should().BeFalse();
        result.Value.Moves.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldShowNoMoves_WhenTheTypeAlreadyFollowsTheWorkflow()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var scheme = Scheme(standard);
        Assign(scheme, standard, null);

        // Act
        var result = await CreateSut().Handle(
            new PreviewWorkflowSchemeEntryQuery(scheme.Id, Bug.Id, standard.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Moves.Should().BeEmpty();
        result.Value.CanMoveRecords.Should().BeTrue();
    }
}
