using System.Security.Cryptography;
using System.Text;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Domain.Tests.Data;

/// <summary>
/// The people poker session tests act as. <see cref="PokerSessionFaker"/> makes <see cref="Facilitator"/> the
/// facilitator of every session it generates.
/// </summary>
public static class PokerSessionActors
{
    public static readonly Guid FacilitatorEmployeeId = Guid.Parse("7a1f0c00-0000-4000-a000-00000000f001");

    /// <summary>The facilitator of a generated session.</summary>
    public static readonly PokerSessionActor Facilitator = new(FacilitatorEmployeeId, "facilitator-user", false, new HashSet<Guid>());

    /// <summary>A poker administrator who is not on any session.</summary>
    public static readonly PokerSessionActor Administrator = new(Guid.Parse("7a1f0c00-0000-4000-a000-00000000a001"), "administrator-user", true, new HashSet<Guid>());

    /// <summary>
    /// Someone acting as <paramref name="userId"/>, with an employee derived from it so the same user is always
    /// the same employee.
    /// </summary>
    public static PokerSessionActor Voter(string userId) =>
        new(EmployeeIdFor(userId), userId, false, new HashSet<Guid>());

    /// <summary>Someone who is the given employee and belongs to <paramref name="teamIds"/>.</summary>
    public static PokerSessionActor Employee(Guid employeeId, params Guid[] teamIds) =>
        new(employeeId, $"user-{employeeId}", false, teamIds.ToHashSet());

    public static Guid EmployeeIdFor(string userId) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes(userId)));
}
