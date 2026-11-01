using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Imports;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Imports;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Imports;

public sealed class IntakeRequestImportDefinitionTests
{
    private const int CreatePass = 0;

    private static readonly Instant SubmittedAt = Instant.FromUtc(2026, 6, 1, 9, 0, 0);
    private static readonly Instant ChangedAt = Instant.FromUtc(2026, 6, 15, 14, 0, 0);

    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IFeatureFlags> _featureFlags = new();
    private readonly IntakeRequestImportDefinition _definition;

    private readonly Employee _requester;
    private readonly Employee _agent;
    private readonly IntakeDesk _desk;

    public IntakeRequestImportDefinitionTests()
    {
        _definition = new IntakeRequestImportDefinition(
            _scenario.DbContext, _scenario.Statuses.Object, _scenario.User.Object, _scenario.Clock, _featureFlags.Object,
            new ImportPayloadSerializer());

        _requester = _scenario.Employee();
        _agent = _scenario.Employee();
        _desk = _scenario.Desk(agentIds: _agent.Id);
    }

    private ImportIntakeRequestDto Row(
        Guid? deskId = null,
        Guid? requesterId = null,
        Guid? channelId = null,
        string? status = null,
        string? statusReason = null,
        Guid? assigneeId = null,
        IReadOnlyList<Guid>? tagIds = null,
        ImportIntakeRequestOriginDto? origin = null,
        Guid? duplicateOfId = null) =>
        new(
            "Export invoices to the new ledger",
            "Finance needs invoices in the new ledger before year end.",
            null,
            deskId ?? _desk.Id,
            channelId,
            requesterId ?? _requester.Id,
            null,
            SubmittedAt,
            null,
            origin,
            status,
            status is null ? null : ChangedAt,
            statusReason,
            duplicateOfId,
            assigneeId,
            tagIds ?? []);

    private ImportProcessRow[] Rows(params ImportIntakeRequestDto[] rows) =>
        [.. rows.Select((r, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(r)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportIntakeRequestDto[] rows) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(rows), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsGatedOnAdministeringDemandManagement()
    {
        // Arrange & Act
        var permission = (_definition.PermissionAction, _definition.PermissionResource);

        // Assert — who acts on a request is a desk role, and the only one spanning every desk is the admin
        permission.Should().Be(("Administer", "IntakeRequests"));
        _definition.Passes.Select(p => p.Name).Should().Equal("CreateIntakeRequests");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsEnabled_FollowsTheDemandManagementFlag(bool flagOn)
    {
        // Arrange
        _featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.DemandManagement, It.IsAny<CancellationToken>())).ReturnsAsync(flagOn);

        // Act
        var enabled = await _definition.IsEnabled(TestContext.Current.CancellationToken);

        // Assert
        enabled.Should().Be(flagOn);
    }

    [Fact]
    public async Task CreateIntakeRequests_CreatesASubmittedRequestForEachRow()
    {
        // Arrange & Act
        var result = await Run(Row(), Row());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _scenario.DbContext.IntakeRequests.Should().HaveCount(2)
            .And.AllSatisfy(r => r.StatusName.Should().Be("Submitted"));
    }

    [Fact]
    public async Task CreateIntakeRequests_RecordsTheSubmissionAtTheRowsTimeAndAsTheSubmitter()
    {
        // Arrange & Act
        await Run(Row());

        // Assert — the history names the person the row is about, not the operator
        var transition = _scenario.DbContext.IntakeRequests.Single().DrainStatusTransitions().Single();
        transition.ChangedOn.Should().Be(SubmittedAt);
        transition.ActorKind.Should().Be(EventActorKind.Import);
        transition.ActorEmployeeId.Should().Be(_requester.Id);
    }

    [Fact]
    public async Task CreateIntakeRequests_MovesTheRequestToAStatusNamedInTheRow()
    {
        // Arrange & Act
        await Run(Row(status: "in review"));

        // Assert
        var request = _scenario.DbContext.IntakeRequests.Single();
        request.StatusName.Should().Be("In Review");
        request.DrainStatusTransitions().Last().ChangedOn.Should().Be(ChangedAt);
    }

    [Fact]
    public async Task CreateIntakeRequests_FindsAStatusByItsMeaningWhenNoneHasTheName()
    {
        // Arrange & Act
        await Run(Row(status: "WaitingOnRequester"));

        // Assert
        _scenario.DbContext.IntakeRequests.Single().StatusName.Should().Be("Waiting on Requester");
    }

    [Fact]
    public async Task CreateIntakeRequests_FindsAMeaningWhateverItsSpacing()
    {
        // Arrange & Act — no status has this name, so it can only be read as the meaning WaitingOnRequester
        await Run(Row(status: "waiting onrequester"));

        // Assert
        _scenario.DbContext.IntakeRequests.Single().StatusName.Should().Be("Waiting on Requester");
    }

    [Theory]
    [InlineData("Submitted,WaitingOnRequester")]
    [InlineData("3")]
    public async Task CreateIntakeRequests_RejectsAStatusThatOnlyParsesAsACombinationOrANumber(string status)
    {
        // Arrange & Act — Enum.TryParse would read the first as Submitted|WaitingOnRequester, which is Accepted
        var result = await Run(Row(status: status));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        _scenario.DbContext.IntakeRequests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Submitted")]
    public async Task CreateIntakeRequests_RejectsAReasonForARequestThatStaysSubmitted(string? status)
    {
        // Arrange & Act — there is no move for the reason to explain, so it would be dropped silently
        var result = await Run(Row(status: status, statusReason: "Parked until the budget is set."));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("StatusReason");
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsAStatusTimeForARequestThatStaysSubmitted()
    {
        // Arrange & Act — Row gives a named status its change time
        var result = await Run(Row(status: "Submitted"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("StatusChangedAt");
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsADuplicateWithNoReason()
    {
        // Arrange
        var survivor = _scenario.Request(_desk, _requester);

        // Act
        var result = await Run(Row(status: "Duplicate", duplicateOfId: survivor.Id));

        // Assert — every closing status but Withdrawn needs a reason the requester can read
        result.Value.Rows.Single().Failed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsAStatusTheDesksWorkflowDoesNotHave()
    {
        // Arrange & Act
        var result = await Run(Row(status: "Parked"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Parked");
        _scenario.DbContext.IntakeRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsADeclineWithNoReason()
    {
        // Arrange & Act
        var result = await Run(Row(status: "Declined"));

        // Assert — the requester is owed a reason, however the request arrived
        result.Value.Rows.Single().Failed.Should().BeTrue();
        _scenario.DbContext.IntakeRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateIntakeRequests_DeclinesWithTheReasonGiven()
    {
        // Arrange & Act
        await Run(Row(status: "Declined", statusReason: "Covered by the ledger project."));

        // Assert
        var request = _scenario.DbContext.IntakeRequests.Single();
        request.StatusName.Should().Be("Declined");
        request.StatusReason.Should().Be("Covered by the ledger project.");
    }

    [Fact]
    public async Task CreateIntakeRequests_RecordsAWithdrawalAsTheRequestersOwnMove()
    {
        // Arrange & Act
        await Run(Row(status: "Withdrawn"));

        // Assert
        var request = _scenario.DbContext.IntakeRequests.Single();
        request.StatusName.Should().Be("Withdrawn");
        request.DrainStatusTransitions().Last().ActorEmployeeId.Should().Be(_requester.Id);
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsADuplicateNamingNoSurvivor()
    {
        // Arrange & Act
        var result = await Run(Row(status: "Duplicate", statusReason: "Asked for already."));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateIntakeRequests_MarksADuplicateOfARequestAlreadyInWayd()
    {
        // Arrange
        var survivor = _scenario.Request(_desk, _requester);

        // Act
        await Run(Row(status: "Duplicate", statusReason: "Asked for already.", duplicateOfId: survivor.Id));

        // Assert
        _scenario.DbContext.IntakeRequests.Single(r => r.Id != survivor.Id).DuplicateOfId.Should().Be(survivor.Id);
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsARowNamingADeskThatDoesNotExist()
    {
        // Arrange
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(deskId: missing));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsARowWhoseRequesterDoesNotExist()
    {
        // Arrange
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(requesterId: missing));

        // Assert
        result.Value.Rows.Single().Error.Should().Contain(missing.ToString());
    }

    [Fact]
    public async Task CreateIntakeRequests_TakesAChannelTheDeskDoesNotOffer()
    {
        // Arrange — an import records where the ask came from, as an upstream system does
        var channel = _scenario.Channel();

        // Act
        var result = await Run(Row(channelId: channel.Id));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _scenario.DbContext.IntakeRequests.Single().ChannelId.Should().Be(channel.Id);
    }

    [Fact]
    public async Task CreateIntakeRequests_AssignsAMemberOfTheDesk()
    {
        // Arrange & Act
        await Run(Row(assigneeId: _agent.Id));

        // Assert
        _scenario.DbContext.IntakeRequests.Single().AssigneeId.Should().Be(_agent.Id);
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsAnAssigneeWhoIsNotOnTheDesk()
    {
        // Arrange & Act
        var result = await Run(Row(assigneeId: _requester.Id));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateIntakeRequests_TagsTheRequestAlongTheDesksAxes()
    {
        // Arrange
        var category = _scenario.TagCategory(allowsMany: true, "Finance", "Compliance");
        var tagIds = category.Tags.Select(t => t.Id).ToList();

        // Act
        await Run(Row(tagIds: tagIds));

        // Assert
        _scenario.DbContext.IntakeRequests.Single().Tags.Select(t => t.TagId).Should().BeEquivalentTo(tagIds);
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsTwoTagsOnAnAxisThatTakesOne()
    {
        // Arrange
        var size = _scenario.TagCategory(allowsMany: false, "Small", "Large");

        // Act
        var result = await Run(Row(tagIds: [.. size.Tags.Select(t => t.Id)]));

        // Assert — tagging replaces the tag on a single-choice axis, so the first would vanish silently
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(size.Name);
        _scenario.DbContext.IntakeRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsAnUnknownTag()
    {
        // Arrange & Act
        var result = await Run(Row(tagIds: [Guid.CreateVersion7()]));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateIntakeRequests_RejectsAnOriginRepeatedWithinTheFile()
    {
        // Arrange
        var origin = new ImportIntakeRequestOriginDto("ServiceNow", "REQ-1001", null);

        // Act
        var result = await Run(Row(origin: origin), Row(origin: origin with { SourceSystem = "servicenow" }));

        // Assert — the unique index compares case-insensitively, and nothing is saved until the chunk is applied
        result.Value.Rows.Select(r => r.Failed).Should().Equal(false, true);
    }
}
