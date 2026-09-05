using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Employees.Imports;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Tests.Sut.Employees.Imports;

public sealed class EmployeeImportDefinitionTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);
    private static readonly Guid _processId = Guid.CreateVersion7();

    private const int CreatePass = 0;
    private const int LinkPass = 1;
    private const int DeactivatePass = 2;

    private readonly FakeWaydDbContext _db = new();
    private readonly EmployeeImportDefinition _definition;

    public EmployeeImportDefinitionTests()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(_now);
        _definition = new EmployeeImportDefinition(_db, clock.Object, new ImportPayloadSerializer());
    }

    private static ImportEmployeeDto Dto(
        string number,
        string? managerNumber = null,
        bool isActive = true,
        string email = "") =>
        new(number, "Avery", null, $"Chen{number}",
            new EmailAddress(string.IsNullOrEmpty(email) ? $"{number}@acme.example" : email),
            _now, "Engineer", "Platform", "Dublin", managerNumber, isActive);

    private ImportProcessRow Row(int rowNumber, ImportEmployeeDto dto) =>
        ImportProcessRow.Create($"r{rowNumber}", rowNumber, _definition.SerializeRow(dto));

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> RunPass(int passIndex, params ImportProcessRow[] rows) =>
        _definition.ExecutePass(_processId, passIndex, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Steps_AreThreeOrderedPassesThatMayAllBeChunked()
    {
        // Arrange & Act
        var passes = _definition.Passes;

        // Assert — the ordering constraint sits between passes, not inside one
        passes.Select(p => p.Name).Should().Equal("CreateEmployees", "LinkManagers", "DeactivateLeavers");
        passes.Should().AllSatisfy(p => p.Scope.Should().Be(ImportPassScope.Chunked));
    }

    [Fact]
    public async Task CreateEmployees_CreatesManagerLessAndReportsTheNewRecord()
    {
        // Arrange — a row naming a manager that does not exist yet
        var row = Row(1, Dto("E-1", managerNumber: "E-9"));

        // Act
        var result = await RunPass(CreatePass, row);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Single().CreatedEntityId.Should().NotBeNull();

        var created = _db.Employees.Single();
        created.EmployeeNumber.Should().Be("E-1");
        created.ManagerId.Should().BeNull();
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateEmployees_RejectsARowWhoseEmployeeNumberIsTaken()
    {
        // Arrange
        var existing = new EmployeeFaker().Generate();
        _db.AddEmployee(existing);
        var row = Row(1, Dto(existing.EmployeeNumber));

        // Act
        var result = await RunPass(CreatePass, row);

        // Assert — named, rather than a duplicate-key error naming neither row nor field
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("employee number");
    }

    [Fact]
    public async Task CreateEmployees_RejectsARowWhoseEmailIsTaken()
    {
        // Arrange
        var existing = new EmployeeFaker().Generate();
        _db.AddEmployee(existing);
        var row = Row(1, Dto("E-new", email: existing.Email.Value));

        // Act
        var result = await RunPass(CreatePass, row);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("email");
    }

    [Fact]
    public async Task CreateEmployees_RejectsTheSecondOfTwoRowsClaimingTheSameAddress()
    {
        // Arrange — neither exists yet, so only an in-chunk check catches this
        var first = Row(1, Dto("E-1", email: "shared@acme.example"));
        var second = Row(2, Dto("E-2", email: "shared@acme.example"));

        // Act
        var result = await RunPass(CreatePass, first, second);

        // Assert — one row rejected rather than the unique index failing the whole chunk
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _db.Employees.Should().ContainSingle();
    }

    [Fact]
    public async Task LinkManagers_ResolvesAManagerCreatedByAnEarlierChunk()
    {
        // Arrange — the manager was created by the previous pass, so it is found by query, not by a
        // dictionary carried between passes
        var manager = Row(1, Dto("E-MGR"));
        var report = Row(2, Dto("E-1", managerNumber: "E-MGR"));
        await RunPass(CreatePass, manager, report);

        // Act
        var result = await RunPass(LinkPass, report);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var managerId = _db.Employees.Single(e => e.EmployeeNumber == "E-MGR").Id;
        _db.Employees.Single(e => e.EmployeeNumber == "E-1").ManagerId.Should().Be(managerId);
    }

    [Fact]
    public async Task LinkManagers_WarnsRatherThanRejectsWhenTheManagerIsUnknown()
    {
        // Arrange
        var row = Row(1, Dto("E-1", managerNumber: "E-NOBODY"));
        await RunPass(CreatePass, row);

        // Act
        var result = await RunPass(LinkPass, row);

        // Assert — the employee is still wanted; the manager may simply not be in Wayd
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();
        outcome.Warning.Should().Contain("manager number");
        _db.Employees.Single().ManagerId.Should().BeNull();
    }

    [Fact]
    public async Task DeactivateLeavers_DeactivatesThroughTheDomainAfterTheLinkPass()
    {
        // Arrange — a leaver who is also somebody's manager
        var leaver = Row(1, Dto("E-MGR", isActive: false));
        var report = Row(2, Dto("E-1", managerNumber: "E-MGR"));
        await RunPass(CreatePass, leaver, report);
        await RunPass(LinkPass, leaver, report);

        // Act
        var result = await RunPass(DeactivatePass, leaver, report);

        // Assert — the link survives the deactivation, which is why this pass runs last
        result.IsSuccess.Should().BeTrue();
        var manager = _db.Employees.Single(e => e.EmployeeNumber == "E-MGR");
        manager.IsActive.Should().BeFalse();
        _db.Employees.Single(e => e.EmployeeNumber == "E-1").ManagerId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task DeactivateLeavers_LeavesActiveRowsAlone()
    {
        // Arrange
        var row = Row(1, Dto("E-1"));
        await RunPass(CreatePass, row);

        // Act
        await RunPass(DeactivatePass, row);

        // Assert
        _db.Employees.Single().IsActive.Should().BeTrue();
    }
}
