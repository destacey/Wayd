using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="ImportTeamsCommandHandler"/> against a real SQL Server container.
///
/// This handler persists teams relationally and then syncs each one into the SQL-graph <c>TeamNodes</c> table
/// via a raw <c>MERGE ... $node_id</c> statement (<see cref="Wayd.Infrastructure.Persistence.Context.WaydDbContext.UpsertTeamNode"/>).
/// That graph path is unrepresentable in SQLite or an in-memory fake, so only a real SQL Server exercises it —
/// which is the core reason this project uses Testcontainers.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ImportTeamsCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ImportTeamsCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private static ImportTeamsCommandHandler CreateHandler(Wayd.Infrastructure.Persistence.Context.WaydDbContext context)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);

        return new ImportTeamsCommandHandler(context, dateTimeProvider.Object, NullLogger<ImportTeamsCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_PersistsTeams_AndSyncsThemIntoTheGraphNodes()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var activeDate = SqlServerDbContextFixture.FixedNow.InUtc().Date;
        var command = new ImportTeamsCommand(
        [
            new ImportTeamDto(TeamType.Team, "Payments", new TeamCode("PAY"), "Owns billing", activeDate),
            new ImportTeamDto(TeamType.TeamOfTeams, "Platform Group", new TeamCode("PLATGRP"), null, activeDate),
        ]);

        // Act
        await using var handlerContext = _fixture.CreateContext();
        var result = await CreateHandler(handlerContext).Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var assertContext = _fixture.CreateContext();

        var teams = await assertContext.BaseTeams.ToListAsync(cancellationToken);
        teams.Select(t => t.Code.Value).Should().BeEquivalentTo(["PAY", "PLATGRP"]);

        // The MERGE landed both teams in the graph node table (queried by the same value-converted Code).
        var nodeCodes = await assertContext.Database
            .SqlQuery<string>($"SELECT [Code] AS [Value] FROM [Organization].[TeamNodes]")
            .ToListAsync(cancellationToken);
        nodeCodes.Should().BeEquivalentTo(["PAY", "PLATGRP"]);
    }
}
