using System.Reflection;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.DemandManagement.Domain.Tests.Data;

public sealed class IntakeDeskFaker : PrivateConstructorFaker<IntakeDesk>
{
    public IntakeDeskFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.PickRandom("Product delivery", "IT", "Data", "Security"));
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.Visibility, IntakeDeskVisibility.Open);
        RuleFor(x => x.IsRequesterSelectable, true);
        RuleFor(x => x.IsOpen, true);
        RuleFor(x => x.RequesterMayAddParticipants, true);
    }
}

public static class IntakeDeskFakerExtensions
{
    private static readonly FieldInfo MembersField =
        typeof(IntakeDesk).GetField("_members", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static IntakeDeskFaker WithId(this IntakeDeskFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static IntakeDeskFaker WithKey(this IntakeDeskFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static IntakeDeskFaker WithName(this IntakeDeskFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static IntakeDeskFaker WithVisibility(this IntakeDeskFaker faker, IntakeDeskVisibility visibility)
    {
        faker.RuleFor(x => x.Visibility, visibility);

        return faker;
    }

    public static IntakeDeskFaker WithIsRequesterSelectable(this IntakeDeskFaker faker, bool isRequesterSelectable)
    {
        faker.RuleFor(x => x.IsRequesterSelectable, isRequesterSelectable);

        return faker;
    }

    public static IntakeDeskFaker WithIsOpen(this IntakeDeskFaker faker, bool isOpen)
    {
        faker.RuleFor(x => x.IsOpen, isOpen);

        return faker;
    }

    public static IntakeDeskFaker WithRequesterMayAddParticipants(this IntakeDeskFaker faker, bool requesterMayAddParticipants)
    {
        faker.RuleFor(x => x.RequesterMayAddParticipants, requesterMayAddParticipants);

        return faker;
    }

    /// <summary>
    /// Names the employees on the desk with their roles, replacing any members it had.
    /// </summary>
    /// <remarks>
    /// Set through the backing list because the domain adds members only at creation until desk
    /// administration arrives, and tests need desks whose members are known.
    /// </remarks>
    public static IntakeDeskFaker WithMembers(this IntakeDeskFaker faker, params IntakeDeskMemberDetails[] members)
    {
        faker.FinishWith((_, desk) =>
        {
            var list = (List<IntakeDeskMember>)MembersField.GetValue(desk)!;
            list.Clear();
            list.AddRange(members.Select(m => new IntakeDeskMember(desk.Id, m.EmployeeId, m.Role)));
        });

        return faker;
    }

    /// <summary>
    /// Names the employees on the desk as agents, replacing any members it had.
    /// </summary>
    public static IntakeDeskFaker WithAgents(this IntakeDeskFaker faker, params Guid[] employeeIds) =>
        faker.WithMembers([.. employeeIds.Select(id => new IntakeDeskMemberDetails(id, IntakeDeskRole.Agent))]);
}
