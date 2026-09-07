using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Imports;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicInitiatives.Imports;

public sealed class StrategicInitiativeImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly StrategicInitiativeImportDefinition _definition;

    private readonly ProjectPortfolio _portfolio;

    public StrategicInitiativeImportDefinitionTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
        _definition = new StrategicInitiativeImportDefinition(_dbContext, new ImportPayloadSerializer());

        // Initiatives can only be created inside an active portfolio.
        _portfolio = ProjectPortfolio.Create("Growth", "Growth portfolio");
        _portfolio.Activate(PpmActor.System, _start);
        _dbContext.AddPortfolio(_portfolio);
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportProcessRow[] Rows(params ImportStrategicInitiativeDto[] initiatives) =>
        [.. initiatives.Select((i, n) => ImportProcessRow.Create($"r{n + 1}", n + 1, _definition.SerializeRow(i)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportStrategicInitiativeDto[] initiatives) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(initiatives), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicWithOnePass()
    {
        // Arrange & Act & Assert — a row depends on nothing else in the file, but the command it replaces
        // saved once, so a rejected row still keeps the whole file out
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreateInitiatives");
    }

    [Fact]
    public async Task CreateInitiatives_CreatesTheInitiativeInThePortfolioTheRowNames()
    {
        // Arrange & Act
        var result = await Run(Row("Expand to EU", StrategicInitiativeStatus.Proposed));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var initiative = _portfolio.StrategicInitiatives.Single();
        initiative.Name.Should().Be("Expand to EU");
        initiative.Status.Should().Be(StrategicInitiativeStatus.Proposed);
        outcome.CreatedEntityId.Should().Be(initiative.Id);
    }

    [Theory]
    [InlineData(StrategicInitiativeStatus.Approved)]
    [InlineData(StrategicInitiativeStatus.Active)]
    [InlineData(StrategicInitiativeStatus.Completed)]
    [InlineData(StrategicInitiativeStatus.Canceled)]
    public async Task CreateInitiatives_DrivesTheInitiativeToItsTargetStatus(StrategicInitiativeStatus status)
    {
        // Arrange & Act — activation only follows approval, so the whole chain is replayed to reach the
        // later statuses
        var result = await Run(Row("Expand to EU", status));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.StrategicInitiatives.Single().Status.Should().Be(status);
    }

    [Fact]
    public async Task CreateInitiatives_RejectsARowAskingForOnHold()
    {
        // Arrange & Act — OnHold is a defined status with no transition that reaches it, so the row is
        // rejected rather than quietly importing a different status
        var result = await Run(Row("Expand to EU", StrategicInitiativeStatus.OnHold));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("on hold");
    }

    [Fact]
    public async Task CreateInitiatives_AttachesProjectsResolvedByKey()
    {
        // Arrange
        var project = CreateProject("APOLLO");

        // Act
        var result = await Run(Row("Expand to EU", StrategicInitiativeStatus.Active) with { ProjectKeys = ["APOLLO"] });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.StrategicInitiatives.Single().StrategicInitiativeProjects
            .Should().ContainSingle(p => p.ProjectId == project.Id);
    }

    [Fact]
    public async Task CreateInitiatives_AttachesProjectsBeforeClosingTheInitiative()
    {
        // Arrange — a closed initiative refuses project changes, so the link has to be made first
        var project = CreateProject("APOLLO");

        // Act
        var result = await Run(Row("Expand to EU", StrategicInitiativeStatus.Completed) with { ProjectKeys = ["APOLLO"] });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var initiative = _portfolio.StrategicInitiatives.Single();
        initiative.Status.Should().Be(StrategicInitiativeStatus.Completed);
        initiative.StrategicInitiativeProjects.Should().ContainSingle(p => p.ProjectId == project.Id);
    }

    [Fact]
    public async Task CreateInitiatives_CreatesTheKpisCarriedOnTheRow()
    {
        // Arrange & Act
        var result = await Run(
            Row("Expand to EU", StrategicInitiativeStatus.Active) with { Kpis = [Kpi("Revenue", 5_000_000)] });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var kpi = _portfolio.StrategicInitiatives.Single().Kpis.Single();
        kpi.Name.Should().Be("Revenue");
        kpi.TargetValue.Should().Be(5_000_000);
    }

    [Fact]
    public async Task CreateInitiatives_CreatesTheKpisBeforeClosingTheInitiative()
    {
        // Arrange & Act — a closed initiative refuses new KPIs, which is why they travel on the row
        var result = await Run(
            Row("Expand to EU", StrategicInitiativeStatus.Completed) with { Kpis = [Kpi("Revenue", 5_000_000)] });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var initiative = _portfolio.StrategicInitiatives.Single();
        initiative.Status.Should().Be(StrategicInitiativeStatus.Completed);
        initiative.Kpis.Should().ContainSingle(k => k.Name == "Revenue");
    }

    [Fact]
    public async Task CreateInitiatives_AssignsRolesResolvedByEmployeeNumber()
    {
        // Arrange
        var sponsor = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        _dbContext.AddEmployee(sponsor);

        // Act
        var result = await Run(
            Row("Expand to EU", StrategicInitiativeStatus.Active) with { SponsorEmployeeNumbers = ["E100"] });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.StrategicInitiatives.Single().Roles
            .Should().ContainSingle(r => r.Role == StrategicInitiativeRole.Sponsor && r.EmployeeId == sponsor.Id);
    }

    [Fact]
    public async Task CreateInitiatives_RejectsARowNamingAProjectThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(Row("Expand to EU", StrategicInitiativeStatus.Active) with { ProjectKeys = ["GEMINI"] });

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("GEMINI");
        _portfolio.StrategicInitiatives.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateInitiatives_RejectsARowNamingAnEmployeeThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(
            Row("Expand to EU", StrategicInitiativeStatus.Active) with { OwnerEmployeeNumbers = ["E999"] });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("E999");
    }

    [Fact]
    public async Task CreateInitiatives_RejectsARowNamingAPortfolioThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(Row("Expand to EU", StrategicInitiativeStatus.Active) with { PortfolioId = Guid.CreateVersion7() });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("portfolio");
    }

    [Fact]
    public async Task CreateInitiatives_RejectsARowWhoseNameIsAlreadyTaken()
    {
        // Arrange — the name is unique, so this used to surface as a constraint violation when the batch
        // saved. Seeded through the set the pass reads: the fake keeps StrategicInitiatives as its own
        // list rather than projecting the portfolios, so creating one here would not be found.
        var existing = SeedExistingInitiative("Expand to EU");
        _dbContext.AddStrategicInitiatives([existing]);

        // Act
        var result = await Run(Row("Expand to EU", StrategicInitiativeStatus.Active));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Expand to EU");
        _portfolio.StrategicInitiatives.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateInitiatives_RejectsTheSecondRowRepeatingANameWithinTheFile()
    {
        // Arrange & Act — the submission command rejects a repeated name before the run starts, but the
        // pass has to hold the rule too: rows are applied one at a time against a portfolio held in
        // memory, so a repeat that reached here would only surface at the unique index
        var result = await Run(
            Row("Expand to EU", StrategicInitiativeStatus.Active),
            Row("Expand to EU", StrategicInitiativeStatus.Active));

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _portfolio.StrategicInitiatives.Should().ContainSingle();
    }

    private Project CreateProject(string key)
    {
        var project = _portfolio.CreateProject(
            $"Project {key}",
            $"{key} description",
            new ProjectKey(key),
            1,
            new LocalDateRange(_start, _end),
            null,
            null,
            null,
            null,
            null,
            _dateTimeProvider.Now, PpmActor.System).Value;

        _dbContext.AddProject(project);

        return project;
    }

    /// <summary>An initiative that already exists, created through a portfolio the import never sees.</summary>
    private StrategicInitiative SeedExistingInitiative(string name)
    {
        var portfolio = ProjectPortfolio.Create("Legacy", "Legacy portfolio");
        portfolio.Activate(PpmActor.System, _start);

        return portfolio.CreateStrategicInitiative(name, $"{name} initiative", new LocalDateRange(_start, _end), []).Value;
    }

    private ImportStrategicInitiativeDto Row(string name, StrategicInitiativeStatus status) =>
        new(name, $"{name} initiative", status, _portfolio.Id, _start, _end, [], [], [], []);

    private static ImportStrategicInitiativeKpiDto Kpi(string name, double targetValue) =>
        new(name, null, targetValue, 0, null, null, KpiTargetDirection.Increase);
}
