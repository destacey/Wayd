using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The team staffing import against a real SQL Server container.
/// </summary>
/// <remarks>
/// The regression guard for the value-object-in-query translation bug: the pass filters teams with
/// <c>.Where(t =&gt; codeValues.Contains(t.Code))</c>. Against a LINQ-to-objects fake that always passes,
/// and so does the equivalent <c>t.Code.Value</c> — which only fails against a real relational provider,
/// because EF can translate the value-converted <c>t.Code</c> but not a member of it. Rewriting the pass
/// to <c>t.Code.Value</c> makes <see cref="ARun_ResolvesTeamsByTheValueConvertedCode"/> fail with a
/// translation error, which is what makes this test guard the bug.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class TeamMemberImportDefinitionTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public TeamMemberImportDefinitionTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private static TeamMemberImportDefinition CreateDefinition(
        Wayd.Infrastructure.Persistence.Context.WaydDbContext context) =>
        new(context, new ImportPayloadSerializer());

    private static ImportProcessRow[] Rows(
        TeamMemberImportDefinition definition,
        params (string TeamCode, string EmployeeNumber, string RoleName)[] members) =>
        [.. members.Select((m, i) => ImportProcessRow.Create(
            $"r{i + 1}", i + 1,
            definition.SerializeRow(new ImportTeamMemberDto(m.TeamCode, m.EmployeeNumber, m.RoleName))))];

    private async Task<ImportProcess> RunImport(
        Wayd.Infrastructure.Persistence.Context.WaydDbContext context,
        TeamMemberImportDefinition definition,
        ImportProcessRow[] rows,
        CancellationToken cancellationToken)
    {
        var process = ImportProcess.Create(
            TeamMemberImportDefinition.ImportKey, "user-1", null, rows, SqlServerDbContextFixture.FixedNow);

        await context.ImportProcesses.AddAsync(process, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);

        var handler = new RunImportProcessCommandHandler(
            context,
            new ImportDefinitionRegistry([definition]),
            clock.Object,
            NullLogger<RunImportProcessCommandHandler>.Instance);

        var result = await handler.Handle(new RunImportProcessCommand(process.Id), cancellationToken);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        return process;
    }

    [Fact]
    public async Task ARun_ResolvesTeamsByTheValueConvertedCode()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using (var seedContext = _fixture.CreateContext())
        {
            await OrganizationSeeder.SeedTeam(seedContext, "PAY", "Payments", cancellationToken);
            await OrganizationSeeder.SeedTeam(seedContext, "PLAT", "Platform", cancellationToken);
            await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);
        }

        // A fresh context, so the pass resolves teams by executing the SQL IN over the varchar column.
        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition, ("PAY", "E-1001", "Engineer"), ("PLAT", "E-1002", "Engineer"));

        // Act
        var process = await RunImport(context, definition, rows, cancellationToken);

        // Assert
        process.Status.Should().Be(ImportProcessStatus.Succeeded);

        await using var assertContext = _fixture.CreateContext();
        var members = await assertContext.TeamMembers
            .Include(m => m.Team)
            .ToListAsync(cancellationToken);

        members.Select(m => m.Team.Code.Value).Should().BeEquivalentTo(["PAY", "PLAT"]);
    }

    [Fact]
    public async Task ARun_AppliesNothingWhenATeamCodeCannotBeResolved()
    {
        // Arrange — the import is atomic, so one unresolvable reference keeps the whole file out
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using (var seedContext = _fixture.CreateContext())
        {
            await OrganizationSeeder.SeedTeam(seedContext, "PAY", "Payments", cancellationToken);
            await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);
        }

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition, ("PAY", "E-1001", "Engineer"), ("NOPE", "E-1001", "Engineer"));

        // Act
        var process = await RunImport(context, definition, rows, cancellationToken);

        // Assert
        process.Status.Should().Be(ImportProcessStatus.Failed);

        await using var assertContext = _fixture.CreateContext();
        (await assertContext.TeamMembers.AnyAsync(cancellationToken)).Should().BeFalse();
    }
}
