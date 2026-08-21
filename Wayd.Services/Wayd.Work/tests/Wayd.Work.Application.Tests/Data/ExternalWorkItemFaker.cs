using Bogus;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Work.Application.Tests.Models;
using NodaTime;

namespace Wayd.Work.Application.Tests.Data;

public class ExternalWorkItemFaker : Faker<ExternalTestWorkItem>
{
    public ExternalWorkItemFaker(Instant? created = null, Instant? lastModified = null)
    {
        var createdInstant = created ?? Instant.FromUtc(2024, 1, 1, 0, 0, 0);
        var lastModifiedInstant = lastModified ?? Instant.FromUtc(2024, 1, 15, 12, 0, 0);

        RuleFor(x => x.Id, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Title, f => f.Lorem.Sentence(5));
        // Use predictable defaults instead of random selection
        RuleFor(x => x.WorkType, "User Story"); // Default to most common type
        RuleFor(x => x.WorkStatus, "New"); // Default to initial status
        RuleFor(x => x.ParentId, f => null); // Default to no parent for simpler tests
        RuleFor(x => x.AssignedTo, f => null); // Default to unassigned
        RuleFor(x => x.Created, f => createdInstant);
        RuleFor(x => x.CreatedBy, f => null); // Default to no creator
        RuleFor(x => x.LastModified, f => lastModifiedInstant);
        RuleFor(x => x.LastModifiedBy, f => null); // Default to no modifier
        RuleFor(x => x.Priority, f => f.Random.Int(1, 4));
        RuleFor(x => x.StackRank, f => f.Random.Double(1000, 100000));
        RuleFor(x => x.ActivatedTimestamp, f => null); // Default to not activated
        RuleFor(x => x.DoneTimestamp, f => null); // Default to not done
        RuleFor(x => x.ExternalTeamIdentifier, f => null);
        RuleFor(x => x.TeamId, f => null);
        RuleFor(x => x.IterationId, f => null);
        RuleFor(x => x.StoryPoints, f => null);
    }
}

public static class ExternalWorkItemFakerExtensions
{
    public static ExternalWorkItemFaker WithId(this ExternalWorkItemFaker faker, int id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static ExternalWorkItemFaker WithTitle(this ExternalWorkItemFaker faker, string title)
    {
        faker.RuleFor(x => x.Title, title);
        return faker;
    }

    public static ExternalWorkItemFaker WithWorkType(this ExternalWorkItemFaker faker, string workType)
    {
        faker.RuleFor(x => x.WorkType, workType);
        return faker;
    }


    public static ExternalWorkItemFaker WithWorkStatus(this ExternalWorkItemFaker faker, string workStatus)
    {
        faker.RuleFor(x => x.WorkStatus, workStatus);
        return faker;
    }

    public static ExternalWorkItemFaker WithParentId(this ExternalWorkItemFaker faker, int? parentId)
    {
        faker.RuleFor(x => x.ParentId, parentId);
        return faker;
    }

    public static ExternalWorkItemFaker WithAssignedTo(this ExternalWorkItemFaker faker, IExternalUserRef? assignedTo)
    {
        faker.RuleFor(x => x.AssignedTo, assignedTo);
        return faker;
    }

    /// <summary>Convenience for the common case: an identity keyed on, and reporting, one address.</summary>
    public static ExternalWorkItemFaker WithAssignedToEmail(this ExternalWorkItemFaker faker, string? email)
    {
        faker.RuleFor(x => x.AssignedTo, email is null ? null : ExternalTestUserRef.FromEmail(email));
        return faker;
    }

    public static ExternalWorkItemFaker WithCreated(this ExternalWorkItemFaker faker, Instant? created)
    {
        faker.RuleFor(x => x.Created, created);
        return faker;
    }

    public static ExternalWorkItemFaker WithCreatedBy(this ExternalWorkItemFaker faker, IExternalUserRef? createdBy)
    {
        faker.RuleFor(x => x.CreatedBy, createdBy);
        return faker;
    }

    /// <summary>Convenience for the common case: an identity keyed on, and reporting, one address.</summary>
    public static ExternalWorkItemFaker WithCreatedByEmail(this ExternalWorkItemFaker faker, string? email)
    {
        faker.RuleFor(x => x.CreatedBy, email is null ? null : ExternalTestUserRef.FromEmail(email));
        return faker;
    }

    public static ExternalWorkItemFaker WithLastModified(this ExternalWorkItemFaker faker, Instant? lastModified)
    {
        faker.RuleFor(x => x.LastModified, lastModified);
        return faker;
    }

    public static ExternalWorkItemFaker WithLastModifiedBy(this ExternalWorkItemFaker faker, IExternalUserRef? lastModifiedBy)
    {
        faker.RuleFor(x => x.LastModifiedBy, lastModifiedBy);
        return faker;
    }

    /// <summary>Convenience for the common case: an identity keyed on, and reporting, one address.</summary>
    public static ExternalWorkItemFaker WithLastModifiedByEmail(this ExternalWorkItemFaker faker, string? email)
    {
        faker.RuleFor(x => x.LastModifiedBy, email is null ? null : ExternalTestUserRef.FromEmail(email));
        return faker;
    }

    public static ExternalWorkItemFaker WithPriority(this ExternalWorkItemFaker faker, int? priority)
    {
        faker.RuleFor(x => x.Priority, priority);
        return faker;
    }

    public static ExternalWorkItemFaker WithStackRank(this ExternalWorkItemFaker faker, double? stackRank)
    {
        faker.RuleFor(x => x.StackRank, stackRank);
        return faker;
    }

    public static ExternalWorkItemFaker WithActivatedTimestamp(this ExternalWorkItemFaker faker, Instant? activatedTimestamp)
    {
        faker.RuleFor(x => x.ActivatedTimestamp, activatedTimestamp);
        return faker;
    }

    public static ExternalWorkItemFaker WithDoneTimestamp(this ExternalWorkItemFaker faker, Instant? doneTimestamp)
    {
        faker.RuleFor(x => x.DoneTimestamp, doneTimestamp);
        return faker;
    }

    public static ExternalWorkItemFaker WithExternalTeamIdentifier(this ExternalWorkItemFaker faker, string? externalTeamIdentifier)
    {
        faker.RuleFor(x => x.ExternalTeamIdentifier, externalTeamIdentifier);
        return faker;
    }

    public static ExternalWorkItemFaker WithTeamId(this ExternalWorkItemFaker faker, Guid? teamId)
    {
        faker.RuleFor(x => x.TeamId, teamId);
        return faker;
    }

    public static ExternalWorkItemFaker WithIterationId(this ExternalWorkItemFaker faker, int? iterationId)
    {
        faker.RuleFor(x => x.IterationId, iterationId);
        return faker;
    }

    public static ExternalWorkItemFaker WithStoryPoints(this ExternalWorkItemFaker faker, int? storyPoints)
    {
        faker.RuleFor(x => x.StoryPoints, storyPoints);
        return faker;
    }

    public static ExternalWorkItemFaker WithTags(this ExternalWorkItemFaker faker, params string[] tags)
    {
        faker.RuleFor(x => x.Tags, tags);
        return faker;
    }
}
