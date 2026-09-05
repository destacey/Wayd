using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Imports.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class GetImportProcessesQueryHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private static readonly string _employeePermission =
        ApplicationPermission.NameFor(ApplicationAction.Import, ApplicationResource.Employees);

    private readonly FakeImportDbContext _db = new();
    private readonly FakeWaydDbContext _waydDb = new();
    private readonly Mock<ICurrentPrincipal> _principal = new();

    private readonly TestImportDefinition _employees = new(new ImportPayloadSerializer());

    private readonly TestImportDefinition _teams = new(new ImportPayloadSerializer())
    {
        KeyOverride = "team-import",
        DisplayNameOverride = "Team Import",
        PermissionResourceOverride = ApplicationResource.Teams,
    };

    public GetImportProcessesQueryHandlerTests()
    {
        // Permitted on employees only, so the two definitions are distinguishable.
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _principal.Setup(p => p.HasPermission(_employeePermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose()
    {
        _db.Dispose();
        _waydDb.Dispose();
    }

    private GetImportProcessesQueryHandler CreateHandler() =>
        new(_db, _waydDb, new ImportDefinitionRegistry([_employees, _teams]), _principal.Object);

    private ImportProcess AddRun(TestImportDefinition definition, Instant submittedOn, string userId = "user-1")
    {
        var rows = new[] { ImportProcessRow.Create("r1", 1, definition.SerializeRow(new TestImportRow("Row 1"))) };
        var process = ImportProcess.Create(definition.Key, userId, null, rows, submittedOn);
        _db.AddImportProcess(process);
        return process;
    }

    private Task<CSharpFunctionalExtensions.Result<ImportProcessPageDto>> Get(
        GetImportProcessesQuery? query = null) =>
        CreateHandler().Handle(query ?? new GetImportProcessesQuery(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ListsOnlyTheTypesTheCallerMaySee()
    {
        // Arrange — one run of each type; the caller holds the employee import permission only
        var mine = AddRun(_employees, _now);
        AddRun(_teams, _now);

        // Act
        var result = await Get();

        // Assert
        result.Value.TotalCount.Should().Be(1);
        result.Value.Processes.Single().Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task Handle_ForACallerPermittedOnNothing_ReturnsAnEmptyPage()
    {
        // Arrange — an empty page, not a failure: the page renders, it just has nothing on it
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        AddRun(_employees, _now);

        // Act
        var result = await Get();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Processes.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForATypeTheCallerCannotSee_FailsWithoutRevealingThatItExists()
    {
        // Arrange
        AddRun(_teams, _now);

        // Act
        var result = await Get(new GetImportProcessesQuery(ImportType: _teams.Key));

        // Assert — the same answer an unknown type gets
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotContain(_teams.DisplayName);
    }

    [Fact]
    public async Task Handle_ReturnsNewestFirst()
    {
        // Arrange
        var older = AddRun(_employees, _now - Duration.FromHours(2));
        var newer = AddRun(_employees, _now);

        // Act
        var result = await Get();

        // Assert
        result.Value.Processes.Select(p => p.Id).Should().Equal(newer.Id, older.Id);
    }

    [Fact]
    public async Task Handle_NarrowsByStatus()
    {
        // Arrange
        var running = AddRun(_employees, _now);
        running.Start("trace-1", _now);
        AddRun(_employees, _now - Duration.FromHours(1));

        // Act
        var result = await Get(new GetImportProcessesQuery(Status: ImportProcessStatus.Processing));

        // Assert
        result.Value.Processes.Single().Id.Should().Be(running.Id);
    }

    [Fact]
    public async Task Handle_NarrowsToOneSubmitter()
    {
        // Arrange
        var mine = AddRun(_employees, _now, userId: "user-1");
        AddRun(_employees, _now, userId: "user-2");

        // Act
        var result = await Get(new GetImportProcessesQuery(SubmittedByUserId: "user-1"));

        // Assert
        result.Value.Processes.Single().Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task Handle_ResolvesTheSubmittersName()
    {
        // Arrange — the column would otherwise show a raw account id, which nobody can act on
        _waydDb.AddWaydUser(new UserFaker().WithId("user-1").WithDisplayName("Dana Reyes").Generate());
        AddRun(_employees, _now, userId: "user-1");

        // Act
        var result = await Get();

        // Assert
        result.Value.Processes.Single().SubmittedByName.Should().Be("Dana Reyes");
    }

    [Fact]
    public async Task Handle_ForASubmitterWithNoAccountRecord_StillReturnsTheRun()
    {
        // Arrange — a deleted account must not hide the history of what it imported
        AddRun(_employees, _now, userId: "long-gone");

        // Act
        var result = await Get();

        // Assert
        var run = result.Value.Processes.Single();
        run.SubmittedByName.Should().BeNull();
        run.SubmittedByUserId.Should().Be("long-gone");
    }

    [Fact]
    public async Task Handle_CarriesWhatTheDefinitionKnowsSoTheCountsCanBeRead()
    {
        // Arrange — "0 of 40 applied" reads as a bug until you know the import is all-or-nothing
        _employees.AtomicityOverride = ImportAtomicity.Atomic;
        AddRun(_employees, _now);

        // Act
        var result = await Get();

        // Assert
        var run = result.Value.Processes.Single();
        run.DisplayName.Should().Be(_employees.DisplayName);
        run.Atomicity.Should().Be(ImportAtomicity.Atomic);
    }

    [Fact]
    public async Task Handle_ClampsAPageSizeNobodyShouldAskFor()
    {
        // Arrange
        AddRun(_employees, _now);

        // Act
        var result = await Get(new GetImportProcessesQuery(PageNumber: 0, PageSize: 100_000));

        // Assert
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(500);
    }
}
