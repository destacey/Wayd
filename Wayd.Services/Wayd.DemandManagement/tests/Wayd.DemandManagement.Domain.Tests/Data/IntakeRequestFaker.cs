using System.Reflection;
using NodaTime;
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

    public static IntakeRequestFaker WithAssignedReviewerId(this IntakeRequestFaker faker, Guid? reviewerId)
    {
        faker.RuleFor(x => x.AssignedReviewerId, reviewerId);

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
