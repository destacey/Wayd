using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Imports;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;
using Wayd.StrategicManagement.Application.StrategicThemes.Imports;
using Wayd.StrategicManagement.Application.Tests.Infrastructure;
using Wayd.StrategicManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.StrategicManagement.Application.Tests.Sut.StrategicThemes.Imports;

public sealed class StrategicThemeImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;

    private readonly FakeStrategicManagementDbContext _dbContext = new();
    private readonly StrategicThemeImportDefinition _definition;

    public StrategicThemeImportDefinitionTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _definition = new StrategicThemeImportDefinition(
            _dbContext, clock, currentUser.Object, new ImportPayloadSerializer());
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportProcessRow[] Rows(params ImportStrategicThemeDto[] themes) =>
        [.. themes.Select((t, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(t)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportStrategicThemeDto[] themes) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(themes), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomic()
    {
        // Arrange & Act & Assert — themes are the name other imports resolve against, so a half-applied
        // file leaves the program and project imports resolving some names and rejecting others
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreateThemes");
    }

    [Theory]
    [InlineData(StrategicThemeState.Proposed)]
    [InlineData(StrategicThemeState.Active)]
    [InlineData(StrategicThemeState.Archived)]
    public async Task CreateThemes_CreatesAThemeInTheRequestedState(StrategicThemeState state)
    {
        // Arrange & Act — Create accepts the state directly, so no activate/archive transition is needed
        var result = await Run(new ImportStrategicThemeDto("Reliability", "Keep the lights on", state));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var theme = _dbContext.StrategicThemes.Single();
        theme.Name.Should().Be("Reliability");
        theme.State.Should().Be(state);
    }

    [Fact]
    public async Task CreateThemes_ReportsTheThemeItCreatedAgainstTheRow()
    {
        // Arrange & Act — the created id is the durable link back, since the import id is not stored on it
        var result = await Run(new ImportStrategicThemeDto("Reliability", "Keep the lights on", StrategicThemeState.Active));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.CreatedEntityId.Should().NotBeNull();
        _dbContext.StrategicThemes.Single().Id.Should().Be(outcome.CreatedEntityId!.Value);
    }

    [Fact]
    public async Task CreateThemes_CreatesEveryRowInTheFile()
    {
        // Arrange & Act
        var result = await Run(
            new ImportStrategicThemeDto("Reliability", "Keep the lights on", StrategicThemeState.Active),
            new ImportStrategicThemeDto("Efficiency", "Do more with less", StrategicThemeState.Active),
            new ImportStrategicThemeDto("Growth", "Reach new markets", StrategicThemeState.Proposed));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.StrategicThemes.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateThemes_RejectsARowWhoseNameIsAlreadyTaken()
    {
        // Arrange — the name is what later imports resolve against, so a reused one would attach their
        // rows to the wrong theme
        _dbContext.AddStrategicTheme(new StrategicThemeFaker().WithName("Reliability").Generate());

        // Act
        var result = await Run(new ImportStrategicThemeDto("Reliability", "Keep the lights on", StrategicThemeState.Active));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Reliability");
        _dbContext.StrategicThemes.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateThemes_KeepsTheGoodRowsWhenOneIsRejected()
    {
        // Arrange — the pass reports per row; the runner is what turns a rejection into a failed run,
        // because this import is atomic
        _dbContext.AddStrategicTheme(new StrategicThemeFaker().WithName("Reliability").Generate());

        // Act
        var result = await Run(
            new ImportStrategicThemeDto("Efficiency", "Do more with less", StrategicThemeState.Active),
            new ImportStrategicThemeDto("Reliability", "Keep the lights on", StrategicThemeState.Active));

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
    }
}
