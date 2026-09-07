using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Programs.Imports;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.Imports;

public sealed class ProgramImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;

    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2025, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly ProgramImportDefinition _definition;

    private readonly ProjectPortfolio _portfolio;

    public ProgramImportDefinitionTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _definition = new ProgramImportDefinition(
            _dbContext, clock, currentUser.Object, new ImportPayloadSerializer());

        // Programs can only be created inside an active portfolio.
        _portfolio = ProjectPortfolio.Create("Growth", "Growth portfolio");
        _portfolio.Activate(PpmActor.System, _start);
        _dbContext.AddPortfolio(_portfolio);
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportProcessRow[] Rows(params ImportProgramDto[] programs) =>
        [.. programs.Select((p, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(p)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportProgramDto[] programs) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(programs), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicWithOnePass()
    {
        // Arrange & Act & Assert — programs are what projects are imported into, so a half-applied file
        // leaves the project import resolving only some parents
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreatePrograms");
    }

    [Fact]
    public async Task CreatePrograms_CreatesTheProgramInThePortfolioTheRowNames()
    {
        // Arrange & Act
        var result = await Run(Row("Platform", ProgramStatus.Active));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var program = _portfolio.Programs.Single();
        program.Name.Should().Be("Platform");
        program.Status.Should().Be(ProgramStatus.Active);
        program.PortfolioId.Should().Be(_portfolio.Id);
        outcome.CreatedEntityId.Should().Be(program.Id);
    }

    [Fact]
    public async Task CreatePrograms_LeavesAProposedRowProposed()
    {
        // Arrange & Act
        var result = await Run(Row("Platform", ProgramStatus.Proposed));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.Programs.Single().Status.Should().Be(ProgramStatus.Proposed);
    }

    [Theory]
    [InlineData(ProgramStatus.Completed)]
    [InlineData(ProgramStatus.Canceled)]
    public async Task CreatePrograms_LeavesATerminalRowActive(ProgramStatus status)
    {
        // Arrange & Act — a program only accepts projects while active and can only close once they are
        // all closed, so the finalize import finishes it after the projects land
        var result = await Run(Row("Platform", status));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.Programs.Single().Status.Should().Be(ProgramStatus.Active);
    }

    [Fact]
    public async Task CreatePrograms_AttachesStrategicThemesResolvedById()
    {
        // Arrange
        var theme = new StrategicThemeFaker().WithName("Reliability").Generate();
        _dbContext.AddPpmStrategicTheme(theme);

        // Act
        var result = await Run(Row("Platform", ProgramStatus.Active) with { StrategicThemeIds = [theme.Id] });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.Programs.Single().StrategicThemeTags.Should().ContainSingle(t => t.StrategicThemeId == theme.Id);
    }

    [Fact]
    public async Task CreatePrograms_RejectsARowNamingAThemeThatIsNotActive()
    {
        // Arrange — only active themes can be attached, so an archived one is reported rather than dropped
        var theme = new StrategicThemeFaker().WithName("Retired").WithState(StrategicThemeState.Archived).Generate();
        _dbContext.AddPpmStrategicTheme(theme);

        // Act
        var result = await Run(Row("Platform", ProgramStatus.Active) with { StrategicThemeIds = [theme.Id] });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(theme.Id.ToString());
        _portfolio.Programs.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePrograms_AssignsRolesResolvedByEmployeeNumber()
    {
        // Arrange
        var manager = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        _dbContext.AddEmployee(manager);

        // Act
        var result = await Run(Row("Platform", ProgramStatus.Active) with { ManagerEmployeeNumbers = ["E100"] });

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _portfolio.Programs.Single().Roles
            .Should().ContainSingle(r => r.Role == ProgramRole.Manager && r.EmployeeId == manager.Id);
    }

    [Fact]
    public async Task CreatePrograms_RejectsARowNamingAPortfolioThatDoesNotExist()
    {
        // Arrange
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row("Platform", ProgramStatus.Active) with { PortfolioId = missing });

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
    }

    [Fact]
    public async Task CreatePrograms_RejectsARowNamingAnEmployeeThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(Row("Platform", ProgramStatus.Active) with { OwnerEmployeeNumbers = ["MISSING"] });

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("MISSING");
    }

    [Fact]
    public async Task CreatePrograms_RejectsTheSecondRowRepeatingANameWithinTheFile()
    {
        // Arrange & Act — the submission command rejects a repeated name before the run starts, but the
        // pass holds the rule too: rows are applied before anything is saved, so a repeat that reached
        // here would only surface at the unique index
        var result = await Run(
            Row("Platform", ProgramStatus.Active),
            Row("platform", ProgramStatus.Active));

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _portfolio.Programs.Should().ContainSingle();
    }

    private ImportProgramDto Row(string name, ProgramStatus status) =>
        new(name, $"{name} program", status, _portfolio.Id, _start, _end, [], [], [], []);
}
