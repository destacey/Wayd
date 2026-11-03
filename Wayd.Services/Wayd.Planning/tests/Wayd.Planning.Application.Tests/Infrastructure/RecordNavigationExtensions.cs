using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Infrastructure;

/// <summary>
/// Fills the navigations EF loads in a projection or include but the in-memory fake leaves null, for the records
/// with people and teams on them.
/// </summary>
public static class RecordNavigationExtensions
{
    /// <summary>
    /// Gives every facilitator, invited employee and member an <see cref="Employee"/>.
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

    /// <summary>
    /// Gives every owner and editor an <see cref="Employee"/>, and every editor team a <see cref="PlanningTeam"/>.
    /// </summary>
    public static StoryMap WithPeopleLinked(this StoryMap map)
    {
        foreach (var owner in map.Owners)
            Link(owner, owner.EmployeeId);
        foreach (var editor in map.Editors)
            Link(editor, editor.EmployeeId);
        foreach (var team in map.EditorTeams)
        {
            var property = typeof(StoryMapEditorTeam).GetProperty(nameof(StoryMapEditorTeam.Team))!;
            if (property.GetValue(team) is null)
                property.SetValue(team, new PlanningTeamFaker(TeamType.Team).WithId(team.TeamId).Generate());
        }

        return map;
    }

    private static void Link(object record, Guid employeeId)
    {
        var property = record.GetType().GetProperty("Employee")!;
        if (property.GetValue(record) is null)
            property.SetValue(record, new EmployeeFaker().WithId(employeeId).Generate());
    }
}
