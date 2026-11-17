using System.Globalization;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Application.Incidents.Imports;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Imports;

/// <summary>
/// Importing incidents. The definition's own work is resolving the selected severity set and the ids a
/// row names, then walking the row's status on the incident it just created.
/// </summary>
public sealed class IncidentImportDefinitionTests
{
    private const int CreatePass = 0;

    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Mock<IFeatureFlags> _featureFlags = new();

    private readonly Guid _submitterId = Guid.CreateVersion7();
    private readonly Guid _workflowId = Guid.CreateVersion7();
    private readonly StatusRef _open;
    private readonly StatusRef _responding;
    private readonly StatusRef _resolved;
    private readonly StatusRef _closed;
    private readonly StatusRef _canceled;
    private readonly IncidentImportDefinition _definition;

    public IncidentImportDefinitionTests()
    {
        ProductWorkflowOwners.Register();

        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_submitterId);
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);
        _featureFlags
            .Setup(f => f.IsEnabled(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _open = Status(ProductStatusAlias.Open, StatusCategory.Active);
        _responding = Status(ProductStatusAlias.Responding, StatusCategory.Active, _open.StatusId);
        _resolved = Status(ProductStatusAlias.Resolved, StatusCategory.Done, _responding.StatusId);
        _closed = Status(ProductStatusAlias.Closed, StatusCategory.Done, _resolved.StatusId);
        _canceled = Status(ProductStatusAlias.Canceled, StatusCategory.Removed, _open.StatusId, _responding.StatusId);

        Stub(_open);
        Stub(_responding);
        Stub(_resolved);
        Stub(_closed);
        Stub(_canceled);

        _definition = new IncidentImportDefinition(
            _dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            _currentPrincipal.Object,
            _dateTimeProvider.Object,
            _featureFlags.Object,
            Mock.Of<ILogger<IncidentImportDefinition>>(),
            new ImportPayloadSerializer());
    }

    [Fact]
    public void Definition_AppliesRowByRow()
    {
        // Arrange & Act & Assert
        _definition.Key.Should().Be("product-management.incidents");
        _definition.DisplayName.Should().Be("Incidents");
        _definition.Atomicity.Should().Be(ImportAtomicity.PerRow);
        _definition.MaxRows.Should().Be(10_000);
        _definition.PreflightMaxRows.Should().Be(10_000);
        _definition.PermissionAction.Should().Be("Import");
        _definition.PermissionResource.Should().Be("Incidents");
        _definition.Passes.Single().Name.Should().Be("CreateIncidents");
        _definition.Passes.Single().Scope.Should().Be(ImportPassScope.Chunked);
    }

    [Fact]
    public async Task IsEnabled_RequiresBothFeatureFlags()
    {
        // Arrange
        _featureFlags
            .Setup(f => f.IsEnabled(FeatureFlags.Names.IncidentManagement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var enabled = await _definition.IsEnabled(TestContext.Current.CancellationToken);

        // Assert
        enabled.Should().BeFalse();
    }

    [Fact]
    public async Task CreateIncidents_OpensWithNoCommanderAndNoLinks()
    {
        // Arrange
        var levelId = SeedSelectedLevel("SEV-1");

        // Act
        var result = await Run(Row());

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        var incident = _dbContext.Incidents.Single();
        incident.StatusAlias.Should().Be(ProductStatusAlias.Open);
        incident.CommanderEmployeeId.Should().BeNull();
        incident.ProductLinks.Should().BeEmpty();
        incident.ReleaseLinks.Should().BeEmpty();
        incident.SubmittedByEmployeeId.Should().Be(_submitterId);
        incident.ReportedByEmployeeId.Should().Be(_submitterId);
        incident.SeverityLevelId.Should().Be(levelId);
        incident.NoticedAt.Should().Be(Now);
        incident.StartedAt.Should().BeNull();
        incident.RecoveredAt.Should().BeNull();
        result.Value.Rows.Single().CreatedEntityId.Should().Be(incident.Id);
    }

    [Fact]
    public async Task CreateIncidents_RejectsALevelFromTheUnselectedSet()
    {
        // Arrange — High exists, on the set that is not selected. SEV-1 is the one new incidents use.
        SeedSelectedLevel("SEV-1");
        var other = IncidentSeveritySet.CreateSystem(
            "Impact",
            null,
            [("High", (string?)IncidentSeverityLevel.DefaultColorForSortOrder(0)), ("Low", IncidentSeverityLevel.DefaultColorForSortOrder(1))]).Value;
        _dbContext.AddIncidentSeveritySet(other);

        // Act
        var result = await Run(Row(importId: "ok"), Row(importId: "other", severity: "High"));

        // Assert
        var outcomes = result.Value.Rows.ToList();
        outcomes[0].Failed.Should().BeFalse();
        outcomes[1].Failed.Should().BeTrue();
        outcomes[1].Error.Should().Be("No severity level named 'High' is in the selected set.");
        _dbContext.Incidents.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateIncidents_RejectsAnUnknownProduct()
    {
        // Arrange
        SeedSelectedLevel();
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(importId: "ok"), Row(importId: "missing", productIds: [missing]));

        // Assert
        var outcomes = result.Value.Rows.ToList();
        outcomes[0].Failed.Should().BeFalse();
        outcomes[1].Failed.Should().BeTrue();
        outcomes[1].Error.Should().Contain(missing.ToString());
        _dbContext.Incidents.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateIncidents_RejectsAnUnknownRelease()
    {
        // Arrange
        SeedSelectedLevel();
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(releaseIds: [missing]));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
        _dbContext.Incidents.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateIncidents_RejectsAnUnknownCommander()
    {
        // Arrange
        SeedSelectedLevel();
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(commanderEmployeeId: missing));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
        _dbContext.Incidents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProductStatusAlias.Responding)]
    [InlineData(ProductStatusAlias.Resolved)]
    [InlineData(ProductStatusAlias.Closed)]
    [InlineData(ProductStatusAlias.Canceled)]
    public async Task CreateIncidents_WalksEachAlias(ProductStatusAlias status)
    {
        // Arrange
        SeedSelectedLevel();

        // Act
        var result = await Run(Row(status: status));

        // Assert — a pass that only creates leaves the incident Open, and leaves the later times empty
        var incident = _dbContext.Incidents.Should().ContainSingle().Subject;
        result.Value.Rows.Single().Failed.Should().BeFalse();
        incident.StatusAlias.Should().Be(status);
        incident.StartedAt.Should().Be(status == ProductStatusAlias.Canceled ? null : Now);
        incident.RecoveredAt.Should().Be(
            status is ProductStatusAlias.Resolved or ProductStatusAlias.Closed ? Now : null);
    }

    [Fact]
    public async Task CreateIncidents_KeepsTheTimesTheRowSupplies()
    {
        // Arrange
        SeedSelectedLevel();
        var noticed = Instant.FromUtc(2026, 3, 1, 8, 0);
        var started = Instant.FromUtc(2026, 3, 1, 8, 15);
        var recovered = Instant.FromUtc(2026, 3, 1, 9, 0);

        // Act
        var result = await Run(Row(
            status: ProductStatusAlias.Closed,
            noticedAt: noticed,
            startedAt: started,
            recoveredAt: recovered));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        var incident = _dbContext.Incidents.Single();
        incident.StatusAlias.Should().Be(ProductStatusAlias.Closed);
        incident.NoticedAt.Should().Be(noticed);
        incident.StartedAt.Should().Be(started);
        incident.RecoveredAt.Should().Be(recovered);
    }

    [Fact]
    public async Task CreateIncidents_RejectsATimelineTheAggregateRefuses()
    {
        // Arrange
        SeedSelectedLevel();

        // Act
        var result = await Run(Row(startedAt: Now.Minus(Duration.FromMinutes(5))));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Be("Response cannot start before the incident was noticed.");
        _dbContext.Incidents.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateIncidents_RejectsABlankImportId_AndKeepsTheOtherRow()
    {
        // Arrange
        SeedSelectedLevel();

        // Act
        var result = await Run(Row(importId: "ok"), Row(importId: "  "));

        // Assert
        var outcomes = result.Value.Rows.ToList();
        outcomes[0].Failed.Should().BeFalse();
        outcomes[1].Failed.Should().BeTrue();
        outcomes[1].Error.Should().Be("ImportId is required.");
        _dbContext.Incidents.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateIncidents_FailsThePass_WhenTheAccountHasNoLinkedEmployee()
    {
        // Arrange
        SeedSelectedLevel();
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        // Act
        var result = await Run(Row());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A linked employee is required to import incidents.");
        _dbContext.Incidents.Should().BeEmpty();
    }

    [Fact]
    public async Task DescribeCreatedRecords_NamesTheIncidentByKeyAndTitle()
    {
        // Arrange
        SeedSelectedLevel();
        await Run(Row(title: "Checkout unavailable"));
        var incident = _dbContext.Incidents.Single();

        // Act
        var links = await _definition.DescribeCreatedRecords([incident.Id], TestContext.Current.CancellationToken);

        // Assert
        var link = links.Should().ContainKey(incident.Id).WhoseValue;
        link.Type.Should().Be(ImportRecordType.Incident);
        link.Key.Should().Be(incident.Key.ToString(CultureInfo.InvariantCulture));
        link.Name.Should().Be("Checkout unavailable");
    }

    private StatusRef Status(ProductStatusAlias alias, StatusCategory category, params Guid[] allowedFrom) =>
        new(_workflowId,
            Guid.CreateVersion7(),
            alias.ToString(),
            category,
            (int)alias,
            allowedFrom.Length == 0 ? null : allowedFrom.ToHashSet());

    private void Stub(StatusRef status) =>
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Incident.Key,
                null,
                status.Alias,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(status));

    private Guid SeedSelectedLevel(string name = "SEV-1")
    {
        var set = IncidentSeveritySet.CreateSystem(
            "SEV",
            null,
            [(name, (string?)IncidentSeverityLevel.DefaultColorForSortOrder(0)), ("SEV-2", IncidentSeverityLevel.DefaultColorForSortOrder(1))]).Value;
        _dbContext.AddIncidentSeveritySet(set);
        _dbContext.AddIncidentSeveritySetAssignment(IncidentSeveritySetAssignment.Create(set.Id));
        return set.Levels.Single(l => l.Name == name).Id;
    }

    private ImportProcessRow[] Rows(params ImportIncidentDto[] incidents) =>
        [.. incidents.Select((incident, i) => ImportProcessRow.Create(
            string.IsNullOrWhiteSpace(incident.ImportId) ? (i + 1).ToString(CultureInfo.InvariantCulture) : incident.ImportId.Trim(),
            i + 1,
            _definition.SerializeRow(incident)))];

    private Task<Result<ImportPassResult>> Run(params ImportIncidentDto[] incidents) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(incidents), isFinalChunk: true, TestContext.Current.CancellationToken);

    private static ImportIncidentDto Row(
        string importId = "i1",
        string title = "Checkout unavailable",
        string severity = "SEV-1",
        ProductStatusAlias status = ProductStatusAlias.Open,
        Guid? commanderEmployeeId = null,
        Guid? reportedByEmployeeId = null,
        Guid[]? productIds = null,
        Guid[]? releaseIds = null,
        Instant? noticedAt = null,
        Instant? startedAt = null,
        Instant? recoveredAt = null) =>
        new(importId,
            title,
            null,
            null,
            null,
            severity,
            status,
            commanderEmployeeId,
            reportedByEmployeeId,
            productIds ?? [],
            releaseIds ?? [],
            noticedAt,
            startedAt,
            recoveredAt);
}
