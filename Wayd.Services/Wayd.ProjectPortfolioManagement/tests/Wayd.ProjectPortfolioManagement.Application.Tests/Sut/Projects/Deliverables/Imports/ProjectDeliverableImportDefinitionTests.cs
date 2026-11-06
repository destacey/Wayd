using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Imports;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Deliverables.Imports;

public sealed class ProjectDeliverableImportDefinitionTests : IDisposable
{
    private const int AddPass = 0;

    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly StatusWorkflow _workflow;
    private readonly ProjectDeliverableImportDefinition _definition;
    private readonly Project _active;
    private readonly Project _completed;

    public ProjectDeliverableImportDefinitionTests()
    {
        _workflow = DeliverableWorkflow.Published(_now);

        var resolver = new Mock<IStatusResolver>();
        resolver
            .Setup(r => r.ForScope(PpmWorkflowOwners.ProjectDeliverable.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_workflow));
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(_now);

        _definition = new ProjectDeliverableImportDefinition(_dbContext, resolver.Object, clock.Object, new ImportPayloadSerializer());

        var dates = new TestingDateTimeProvider(new FakeClock(_now));
        _active = new ProjectFaker().WithKey(new ProjectKey("APOLLO")).AsActive(dates);
        _completed = new ProjectFaker().WithKey(new ProjectKey("GEMINI")).AsCompleted(dates);
        _dbContext.AddProjects([_active, _completed]);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ImportProjectDeliverableDto Row(string projectKey, string name, string? status = null, string? reason = null, string? owner = null) =>
        new(new ProjectKey(projectKey), name, null, null, owner, null, status, null, reason);

    private Task<Result<ImportPassResult>> Run(params ImportProjectDeliverableDto[] rows) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), AddPass,
            [.. rows.Select((r, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(r)))],
            isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_AppliesProjectByProject()
    {
        // Arrange
        var row = Row("APOLLO", "Portal");

        // Act
        var key = _definition.GroupKeysOf([("r1", _definition.SerializeRow(row))]).Single();

        // Assert
        _definition.Atomicity.Should().Be(ImportAtomicity.PerGroup);
        key.Should().Be("APOLLO");
    }

    [Fact]
    public async Task AddDeliverables_AddsRowsInFileOrderNotStartedByDefault()
    {
        // Arrange
        var owner = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        _dbContext.AddEmployee(owner);

        // Arrange & Act
        var result = await Run(Row("APOLLO", "Portal", owner: "E100"), Row("apollo", "Reports"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _active.Deliverables.OrderBy(d => d.Order).Select(d => d.Name).Should().Equal("Portal", "Reports");
        _active.Deliverables.Should().AllSatisfy(d => d.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.NotStarted));
        _active.Deliverables.Single(d => d.Name == "Portal").OwnerId.Should().Be(owner.Id);
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("accepted")]
    public async Task AddDeliverables_ResolvesAStatusByNameOrMeaning(string status)
    {
        // Arrange & Act
        var result = await Run(Row("GEMINI", "Portal", status));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _completed.Deliverables.Single().StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
    }

    [Fact]
    public async Task AddDeliverables_ResolvesAMeaningWrittenWithoutSpaces()
    {
        // Arrange & Act
        var result = await Run(Row("APOLLO", "Portal", "InProgress"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _active.Deliverables.Single().StatusAlias.Should().Be(ProjectDeliverableStatusAlias.InProgress);
    }

    [Fact]
    public async Task AddDeliverables_RejectsAnOpenDeliverableOnACompletedProject()
    {
        // Arrange & Act
        var result = await Run(Row("GEMINI", "Portal"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("still open");
        _completed.Deliverables.Should().BeEmpty();
    }

    [Theory]
    [InlineData("NOPE", null, null, "No project")]
    [InlineData("APOLLO", "Shipped", null, "no status named or meaning 'Shipped'")]
    [InlineData("APOLLO", "Descoped", null, "needs a reason")]
    public async Task AddDeliverables_RejectsARowItCannotPlace(string projectKey, string? status, string? owner, string error)
    {
        // Arrange & Act
        var result = await Run(Row(projectKey, "Portal", status, owner: owner));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(error);
    }

    [Fact]
    public async Task AddDeliverables_RejectsAnUnknownOwner()
    {
        // Arrange & Act
        var result = await Run(Row("APOLLO", "Portal", owner: "E999"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("E999");
        _active.Deliverables.Should().BeEmpty();
    }

    [Fact]
    public async Task AddDeliverables_AcceptsAnInactiveOwner()
    {
        // Arrange
        var owner = new EmployeeFaker().WithEmployeeNumber("E200").WithIsActive(false).Generate();
        _dbContext.AddEmployee(owner);

        // Act
        var result = await Run(Row("GEMINI", "Portal", "Accepted", owner: "E200"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _completed.Deliverables.Single().OwnerId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task AddDeliverables_RejectsAStatusChangeInTheFuture()
    {
        // Arrange
        var row = Row("APOLLO", "Portal", "InProgress") with { StatusChangedAt = _now.Plus(Duration.FromMinutes(1)) };

        // Act
        var result = await Run(row);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("in the future");
        _active.Deliverables.Should().BeEmpty();
    }

    [Fact]
    public async Task AddDeliverables_RecordsAStatusChangeInThePastAtThatTime()
    {
        // Arrange
        var changedAt = _now.Minus(Duration.FromDays(3));
        var row = Row("APOLLO", "Portal", "InProgress") with { StatusChangedAt = changedAt };

        // Act
        var result = await Run(row);

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _active.Deliverables.Single().StatusTransitions.Should().ContainSingle().Which.ChangedOn.Should().Be(changedAt);
    }

    [Fact]
    public async Task AddDeliverables_RejectsANameTheProjectAlreadyUses()
    {
        // Arrange
        _active.AddDeliverable(Domain.Models.Authorization.PpmActor.System, Domain.Models.Authorization.ProjectAncestryRoles.None,
            "Portal", null, null, null, null, _workflow.Ref(ProjectDeliverableStatusAlias.NotStarted), _now);

        // Act
        var result = await Run(Row("APOLLO", "PORTAL"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("already has a deliverable named");
        _active.Deliverables.Should().ContainSingle();
    }
}
