using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Imports;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Imports;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.Risks.Imports;

public sealed class RiskImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;

    private static readonly Instant ReportedOn = Instant.FromUtc(2026, 6, 1, 9, 0, 0);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly RiskImportDefinition _definition;

    private readonly PlanningTeam _team;
    private readonly Employee _reporter;

    public RiskImportDefinitionTests()
    {
        _definition = new RiskImportDefinition(_dbContext, new ImportPayloadSerializer());

        _team = new PlanningTeamFaker(TeamType.Team).Generate();
        _reporter = new EmployeeFaker().Generate();

        _dbContext.AddPlanningTeam(_team);
        _dbContext.AddEmployee(_reporter);
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportRiskDto Row(Guid? teamId = null, Guid? reportedById = null, Guid? assigneeId = null) =>
        new(
            "A risk worth recording",
            "Something might go wrong.",
            teamId ?? _team.Id,
            ReportedOn,
            reportedById ?? _reporter.Id,
            RiskStatus.Open,
            RiskCategory.Owned,
            RiskGrade.Medium,
            RiskGrade.Medium,
            assigneeId,
            null,
            null,
            null);

    private ImportProcessRow[] Rows(params ImportRiskDto[] risks) =>
        [.. risks.Select((r, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(r)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportRiskDto[] risks) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(risks), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicWithOnePass()
    {
        // Arrange & Act
        var passes = _definition.Passes;

        // Assert — the command this replaced saved once for the whole file, so a bad row wrote nothing
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        passes.Select(p => p.Name).Should().Equal("CreateRisks");
    }

    [Fact]
    public async Task CreateRisks_AddsARiskForEachRow()
    {
        // Arrange & Act
        var result = await Run(Row(), Row());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.Risks.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateRisks_ReportsTheRiskItCreatedAgainstTheRow()
    {
        // Arrange & Act — the created id is the durable link back, since the import id is not stored on it
        var result = await Run(Row());

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.CreatedEntityId.Should().NotBeNull();
        _dbContext.Risks.Single().Id.Should().Be(outcome.CreatedEntityId!.Value);
    }

    [Fact]
    public async Task CreateRisks_RejectsARowNamingATeamThatDoesNotExist()
    {
        // Arrange
        var missingTeamId = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(teamId: missingTeamId));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missingTeamId.ToString());
        _dbContext.Risks.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRisks_RejectsARowWhoseReporterDoesNotExist()
    {
        // Arrange — a real foreign key, so this used to surface as a constraint violation at save
        var missingEmployeeId = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(reportedById: missingEmployeeId));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("report");
        _dbContext.Risks.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRisks_RejectsARowWhoseAssigneeDoesNotExist()
    {
        // Arrange
        var missingEmployeeId = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(assigneeId: missingEmployeeId));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("assign");
    }

    [Fact]
    public async Task CreateRisks_AcceptsARowWithNoAssignee()
    {
        // Arrange & Act — the assignee is optional, and absent must not read as unknown
        var result = await Run(Row(assigneeId: null));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Risks.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateRisks_KeepsTheGoodRowsWhenOneIsRejected()
    {
        // Arrange — the pass reports per row; the runner is what turns a rejection into a failed run,
        // because this import is atomic
        var rows = Rows(Row(), Row(teamId: Guid.CreateVersion7()));

        // Act
        var result = await _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
    }
}
