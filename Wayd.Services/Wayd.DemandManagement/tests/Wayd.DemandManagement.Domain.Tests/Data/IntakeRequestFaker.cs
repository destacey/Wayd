using System.Reflection;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.DemandManagement.Domain.Tests.Data;

/// <summary>
/// A persisted intake request: it has a <see cref="IntakeRequest.Key"/>, so its changes raise their events
/// immediately rather than after the first save.
/// </summary>
public sealed class IntakeRequestFaker : PrivateConstructorFaker<IntakeRequest>
{
    public IntakeRequestFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Title, f => f.Lorem.Sentence(5));
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.ExpectedBenefits, f => f.Lorem.Sentence());
        RuleFor(x => x.RequesterId, f => f.Random.Guid());
        RuleFor(x => x.SubmittedById, (_, r) => r.RequesterId);
        RuleFor(x => x.DeskId, f => f.Random.Guid());
        RuleFor(x => x.ChannelId, f => f.Random.Guid());
        // As submitted: in the status meaning Submitted, with that one transition recorded.
        RuleFor(x => x.StatusId, f => f.Random.Guid());
        RuleFor(x => x.StatusWorkflowId, f => f.Random.Guid());
        RuleFor(x => x.StatusName, _ => "Submitted");
        RuleFor(x => x.StatusCategory, _ => StatusCategory.Proposed);
        RuleFor(x => x.StatusAliasValue, _ => (int)IntakeRequestStatusAlias.Submitted);
        RuleFor(x => x.StatusTransitionCount, _ => 1);
    }
}

public static class IntakeRequestFakerExtensions
{
    private static readonly FieldInfo ParticipantsField =
        typeof(IntakeRequest).GetField("_participants", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static IntakeRequestFaker WithRequesterId(this IntakeRequestFaker faker, Guid requesterId)
    {
        faker.RuleFor(x => x.RequesterId, requesterId);

        return faker;
    }

    public static IntakeRequestFaker WithSubmittedById(this IntakeRequestFaker faker, Guid submittedById)
    {
        faker.RuleFor(x => x.SubmittedById, submittedById);

        return faker;
    }

    public static IntakeRequestFaker WithDeskId(this IntakeRequestFaker faker, Guid deskId)
    {
        faker.RuleFor(x => x.DeskId, deskId);

        return faker;
    }

    public static IntakeRequestFaker WithChannelId(this IntakeRequestFaker faker, Guid channelId)
    {
        faker.RuleFor(x => x.ChannelId, channelId);

        return faker;
    }

    public static IntakeRequestFaker WithNeedBy(this IntakeRequestFaker faker, IntakeRequestNeedBy? needBy)
    {
        faker.RuleFor(x => x.NeedBy, needBy);

        return faker;
    }

    /// <summary>
    /// Puts the request in a status of the category, with the alias, and one more recorded transition than it
    /// was submitted with, as if it had been moved there.
    /// </summary>
    public static IntakeRequestFaker InStatus(this IntakeRequestFaker faker, StatusCategory category,
        IntakeRequestStatusAlias alias = IntakeRequestStatusAlias.None) =>
        faker.WithStatusCategory(category).WithStatusAlias(alias).WithStatusTransitionCount(2);

    public static IntakeRequestFaker WithStatusId(this IntakeRequestFaker faker, Guid statusId)
    {
        faker.RuleFor(x => x.StatusId, statusId);

        return faker;
    }

    public static IntakeRequestFaker WithStatusWorkflowId(this IntakeRequestFaker faker, Guid workflowId)
    {
        faker.RuleFor(x => x.StatusWorkflowId, workflowId);

        return faker;
    }

    public static IntakeRequestFaker WithStatusCategory(this IntakeRequestFaker faker, StatusCategory category)
    {
        faker.RuleFor(x => x.StatusCategory, category);

        return faker;
    }

    public static IntakeRequestFaker WithStatusAlias(this IntakeRequestFaker faker, IntakeRequestStatusAlias alias)
    {
        faker.RuleFor(x => x.StatusAliasValue, (int)alias);

        return faker;
    }

    public static IntakeRequestFaker WithStatusTransitionCount(this IntakeRequestFaker faker, int count)
    {
        faker.RuleFor(x => x.StatusTransitionCount, count);

        return faker;
    }

    public static IntakeRequestFaker WithAssigneeId(this IntakeRequestFaker faker, Guid? assigneeId)
    {
        faker.RuleFor(x => x.AssigneeId, assigneeId);

        return faker;
    }

    /// <summary>
    /// Gives the employees access as participants, added by the platform.
    /// </summary>
    public static IntakeRequestFaker WithParticipants(this IntakeRequestFaker faker, params Guid[] employeeIds)
    {
        faker.FinishWith((_, request) =>
        {
            var participants = (List<IntakeRequestParticipant>)ParticipantsField.GetValue(request)!;
            participants.Clear();
            participants.AddRange(employeeIds.Select(id => new IntakeRequestParticipant(request.Id, id, null, Instant.MinValue)));
        });

        return faker;
    }
}
