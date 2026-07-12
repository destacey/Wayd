using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class ImportTeamMembershipsCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider =
        new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate MembershipStart = new(2024, 6, 1);

    private ImportTeamMembershipsCommandHandler CreateHandler() =>
        new(_dbContext, _dateTimeProvider, NullLogger<ImportTeamMembershipsCommandHandler>.Instance);

    private Team SeedTeam(string code)
    {
        var team = new TeamFaker().WithCode(new TeamCode(code)).WithActiveDate(ActiveDate).AsActive().Generate();
        _dbContext.AddTeam(team);
        return team;
    }

    private TeamOfTeams SeedTeamOfTeams(string code)
    {
        var tot = new TeamOfTeamsFaker().WithCode(new TeamCode(code)).WithActiveDate(ActiveDate).AsActive().Generate();
        _dbContext.AddTeamOfTeams(tot);
        return tot;
    }

    private static ImportTeamMembershipDto Row(string childCode, string parentCode) =>
        new(childCode, parentCode, MembershipStart, End: null);

    [Fact]
    public async Task Handle_PlacesTeamUnderTeamOfTeams()
    {
        // Arrange
        var team = SeedTeam("PAY");
        var art = SeedTeamOfTeams("PAYART");

        var command = new ImportTeamMembershipsCommand([Row("pay", "payart")]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.ParentMemberships.Should().ContainSingle();
        team.ParentMemberships.Single().TargetId.Should().Be(art.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _dbContext.UpsertTeamMembershipEdgeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NestsTeamOfTeamsUnderTeamOfTeams()
    {
        // Arrange — a three-tier hierarchy: an ART (ToT) placed under a value stream (ToT).
        var art = SeedTeamOfTeams("ART");
        var valueStream = SeedTeamOfTeams("VS");

        var command = new ImportTeamMembershipsCommand([Row("ART", "VS")]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        art.ParentMemberships.Should().ContainSingle();
        art.ParentMemberships.Single().TargetId.Should().Be(valueStream.Id);
    }

    [Fact]
    public async Task Handle_ImportsFullThreeTierBatch()
    {
        // Arrange — Team -> ART -> Value Stream, all in one batch.
        SeedTeam("PAY");
        SeedTeamOfTeams("PAYART");
        SeedTeamOfTeams("PAYVS");

        var command = new ImportTeamMembershipsCommand(
        [
            Row("PAY", "PAYART"),
            Row("PAYART", "PAYVS"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _dbContext.UpsertTeamMembershipEdgeCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_Fails_WhenChildCodeUnresolved()
    {
        // Arrange
        SeedTeamOfTeams("PAYART");

        var command = new ImportTeamMembershipsCommand([Row("NOPE", "PAYART")]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("team code 'NOPE'");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenParentIsNotATeamOfTeams()
    {
        // Arrange — parent code resolves to a plain Team, which cannot be a parent.
        SeedTeam("PAY");
        SeedTeam("OTHER");

        var command = new ImportTeamMembershipsCommand([Row("PAY", "OTHER")]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not Teams of Teams");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
