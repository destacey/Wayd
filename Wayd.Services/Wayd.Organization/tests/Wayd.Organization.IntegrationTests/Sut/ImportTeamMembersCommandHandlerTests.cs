using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="ImportTeamMembersCommandHandler"/> against a real SQL Server container.
///
/// This is the regression guard for the value-object-in-query translation bug: the handler filters teams with
/// <c>.Where(t =&gt; teamCodeValues.Contains(t.Code))</c>. Against LINQ-to-objects fakes that always passes,
/// but the equivalent <c>t.Code.Value</c> only fails against a real relational provider — EF can translate the
/// value-converted <c>t.Code</c> but not <c>t.Code.Value</c>. Reverting the handler to <c>t.Code.Value</c>
/// makes <see cref="Handle_AddsMembers_FilteringTeamsByValueConvertedCode"/> fail with a translation error,
/// which is exactly what makes this test guard the bug.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ImportTeamMembersCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ImportTeamMembersCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_AddsMembers_FilteringTeamsByValueConvertedCode()
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

        var command = new ImportTeamMembersCommand(
        [
            new ImportTeamMemberDto("PAY", "E-1001", "Engineer"),
            new ImportTeamMemberDto("PLAT", "E-1002", "Engineer"),
        ]);

        // Act — a fresh context so the handler resolves teams by executing the SQL IN over the varchar column.
        await using var handlerContext = _fixture.CreateContext();
        var handler = new ImportTeamMembersCommandHandler(handlerContext, NullLogger<ImportTeamMembersCommandHandler>.Instance);
        var result = await handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var assertContext = _fixture.CreateContext();
        var members = await assertContext.TeamMembers
            .Include(m => m.Team)
            .Include(m => m.Employee)
            .ToListAsync(cancellationToken);

        members.Should().HaveCount(2);
        members.Select(m => m.Team.Code.Value).Should().BeEquivalentTo(["PAY", "PLAT"]);
    }

    [Fact]
    public async Task Handle_Fails_WhenTeamCodeUnresolved()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using (var seedContext = _fixture.CreateContext())
        {
            await OrganizationSeeder.SeedTeam(seedContext, "PAY", "Payments", cancellationToken);
            await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);
        }

        var command = new ImportTeamMembersCommand([new ImportTeamMemberDto("NOPE", "E-1001", "Engineer")]);

        // Act
        await using var handlerContext = _fixture.CreateContext();
        var handler = new ImportTeamMembersCommandHandler(handlerContext, NullLogger<ImportTeamMembersCommandHandler>.Instance);
        var result = await handler.Handle(command, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("team code 'NOPE'");

        await using var assertContext = _fixture.CreateContext();
        (await assertContext.TeamMembers.AnyAsync(cancellationToken)).Should().BeFalse();
    }
}
