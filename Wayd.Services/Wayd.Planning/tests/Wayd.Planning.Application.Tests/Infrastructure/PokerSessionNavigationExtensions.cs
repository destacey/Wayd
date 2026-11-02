using Wayd.Common.Domain.Employees;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Infrastructure;

public static class PokerSessionNavigationExtensions
{
    /// <summary>
    /// Gives every facilitator, invited employee and member an <see cref="Employee"/>, which EF loads in a
    /// projection but the in-memory fake leaves null.
    /// </summary>
    public static PokerSession WithEmployeesLinked(this PokerSession session)
    {
        foreach (var facilitator in session.Facilitators)
            Link(facilitator, facilitator.EmployeeId);
        foreach (var invitee in session.InvitedEmployees)
            Link(invitee, invitee.EmployeeId);
        foreach (var member in session.Members)
            Link(member, member.EmployeeId);

        return session;
    }

    private static void Link(object record, Guid employeeId)
    {
        var property = record.GetType().GetProperty("Employee")!;
        if (property.GetValue(record) is null)
            property.SetValue(record, new EmployeeFaker().WithId(employeeId).Generate());
    }
}
