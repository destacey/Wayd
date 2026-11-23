using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Identity;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.IntegrationTests.Infrastructure;

/// <summary>
/// An Active, Public poker session with one round in voting, and a voter who is not yet a member: an employee
/// linked to a user account, since members point at employees and votes at users.
/// </summary>
internal sealed record PokerSeed(Guid SessionId, Guid RoundId, PokerSessionActor Voter)
{
    /// <summary>Seeds the scale, the facilitator, the voter and the session through their own domains.</summary>
    public static async Task<PokerSeed> ActiveSession(SqlServerDbContextFixture fixture, CancellationToken ct)
    {
        var now = SqlServerDbContextFixture.FixedNow;
        await using var context = fixture.CreateContext();

        var facilitator = Employee(now);
        var voter = Employee(now);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = $"voter.{suffix}@acme.example",
            NormalizedUserName = $"VOTER.{suffix}@ACME.EXAMPLE",
            Email = $"voter.{suffix}@acme.example",
            NormalizedEmail = $"VOTER.{suffix}@ACME.EXAMPLE",
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true,
            LoginProvider = LoginProviders.Wayd,
            EmployeeId = voter.Id,
        };
        context.Employees.AddRange(facilitator, voter);
        context.Set<ApplicationUser>().Add(user);

        var scale = EstimationScale.Create($"Sizes {Guid.NewGuid():N}"[..20], null,
            [new("S", true), new("M", true), new("L", true), new("?", false)], EventActor.System, now).Value;
        if (!scale.IsActive)
            scale.Activate(EventActor.System, now).IsSuccess.Should().BeTrue();
        context.EstimationScales.Add(scale);
        await context.SaveChangesAsync(ct);

        var session = PokerSession.Create("Refinement", scale, Visibility.Public, facilitator.Id, now).Value;
        var facilitatorActor = new PokerSessionActor(facilitator.Id, "facilitator-user", false, new HashSet<Guid>());
        var round = session.AddRound("Story", null, facilitatorActor).Value;
        context.PokerSessions.Add(session);
        await context.SaveChangesAsync(ct);

        return new PokerSeed(session.Id, round.Id, new PokerSessionActor(voter.Id, user.Id, false, new HashSet<Guid>()));
    }

    /// <summary>Loads the session as the handlers do, tracked, with its members and votes.</summary>
    public static Task<PokerSession> Load(WaydDbContext context, Guid sessionId, CancellationToken ct) =>
        context.PokerSessions
            .IncludeAccess()
            .Include(s => s.Members)
            .Include(s => s.Rounds).ThenInclude(r => r.Votes)
            .SingleAsync(s => s.Id == sessionId, ct);

    private static Employee Employee(Instant now) =>
        Common.Domain.Employees.Employee.Create(new PersonName("Ada", null, "Lovelace"), "E" + Guid.NewGuid().ToString("N")[..11], now,
            new EmailAddress($"ada.{Guid.NewGuid():N}@acme.example"), null, null, null, null, true, null, EventActor.System, now);
}
