using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Imports;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Imports;

public sealed class ProjectPortfolioImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2026, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly ProjectPortfolioImportDefinition _definition;

    public ProjectPortfolioImportDefinitionTests()
    {
        _definition = new ProjectPortfolioImportDefinition(_dbContext, new ImportPayloadSerializer());
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportProcessRow[] Rows(params ImportProjectPortfolioDto[] portfolios) =>
        [.. portfolios.Select((p, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(p)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportProjectPortfolioDto[] portfolios) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(portfolios), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicWithOnePass()
    {
        // Arrange & Act & Assert — portfolios are what programs, projects and initiatives are imported
        // into, so a half-applied file leaves the rest of a PPM import resolving only some parents
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreatePortfolios");
    }

    [Fact]
    public async Task CreatePortfolios_CreatesAProposedPortfolioWithoutDates()
    {
        // Arrange & Act
        var result = await Run(Row("Growth", ProjectPortfolioStatus.Proposed, start: null));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var portfolio = _dbContext.Portfolios.Single();
        portfolio.Name.Should().Be("Growth");
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Proposed);
        portfolio.DateRange.Should().BeNull();
        outcome.CreatedEntityId.Should().Be(portfolio.Id);
    }

    [Fact]
    public async Task CreatePortfolios_ActivatesWithTheRowsOwnStartDate()
    {
        // Arrange & Act — the row's date must win: the activate command hardcodes today, which would
        // flatten the historical timeline
        var result = await Run(Row("Growth", ProjectPortfolioStatus.Active, _start));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var portfolio = _dbContext.Portfolios.Single();
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Active);
        portfolio.DateRange!.Start.Should().Be(_start);
    }

    [Fact]
    public async Task CreatePortfolios_PausesAPortfolioImportedOnHold()
    {
        // Arrange & Act
        var result = await Run(Row("Growth", ProjectPortfolioStatus.OnHold, _start));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Portfolios.Single().Status.Should().Be(ProjectPortfolioStatus.OnHold);
    }

    [Theory]
    [InlineData(ProjectPortfolioStatus.Closed)]
    [InlineData(ProjectPortfolioStatus.Archived)]
    public async Task CreatePortfolios_LeavesATerminalRowActive(ProjectPortfolioStatus status)
    {
        // Arrange & Act — a portfolio cannot close until its contents are closed, so the finalize import
        // finishes the job once they have landed
        var result = await Run(Row("Growth", status, _start, _end));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var portfolio = _dbContext.Portfolios.Single();
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Active);
        portfolio.DateRange!.Start.Should().Be(_start);
    }

    [Fact]
    public async Task CreatePortfolios_AssignsRolesResolvedByEmployeeNumber()
    {
        // Arrange
        var sponsor = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        var owner = new EmployeeFaker().WithEmployeeNumber("E200").Generate();
        _dbContext.AddEmployees([sponsor, owner]);

        // Act
        var result = await Run(Row("Growth", ProjectPortfolioStatus.Active, _start) with
        {
            SponsorEmployeeNumbers = ["E100"],
            OwnerEmployeeNumbers = ["E200"],
        });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var roles = _dbContext.Portfolios.Single().Roles;
        roles.Should().ContainSingle(r => r.Role == ProjectPortfolioRole.Sponsor && r.EmployeeId == sponsor.Id);
        roles.Should().ContainSingle(r => r.Role == ProjectPortfolioRole.Owner && r.EmployeeId == owner.Id);
    }

    [Fact]
    public async Task CreatePortfolios_RejectsARowNamingAnEmployeeThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(
            Row("Growth", ProjectPortfolioStatus.Active, _start) with { OwnerEmployeeNumbers = ["MISSING"] });

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("MISSING");
        _dbContext.Portfolios.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePortfolios_RejectsARowWhoseNameIsAlreadyTaken()
    {
        // Arrange — names are what other imports resolve against, so a collision has to be rejected
        // rather than silently creating a second portfolio of the same name
        _dbContext.AddPortfolio(new ProjectPortfolioFaker().WithName("Growth").Generate());

        // Act
        var result = await Run(Row("Growth", ProjectPortfolioStatus.Active, _start));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Growth");
    }

    [Fact]
    public async Task CreatePortfolios_RejectsTheSecondRowRepeatingANameWithinTheFile()
    {
        // Arrange & Act — the submission command rejects a repeated name before the run starts, but the
        // pass holds the rule too: rows are applied before anything is saved, so a repeat that reached
        // here would only surface at the unique index
        var result = await Run(
            Row("Growth", ProjectPortfolioStatus.Active, _start),
            Row("growth", ProjectPortfolioStatus.Active, _start));

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _dbContext.Portfolios.Should().ContainSingle();
    }

    [Fact]
    public async Task CreatePortfolios_CreatesEveryRowInTheFile()
    {
        // Arrange & Act
        var result = await Run(
            Row("Growth", ProjectPortfolioStatus.Active, _start),
            Row("Efficiency", ProjectPortfolioStatus.Proposed, start: null),
            Row("Platform", ProjectPortfolioStatus.OnHold, _start));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.Portfolios.Should().HaveCount(3);
    }

    private static ImportProjectPortfolioDto Row(
        string name, ProjectPortfolioStatus status, LocalDate? start, LocalDate? end = null) =>
        new(name, $"{name} portfolio", status, start, end, [], [], []);
}
