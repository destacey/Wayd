using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkTypeFaker : PrivateConstructorFaker<WorkType>
{
    public WorkTypeFaker()
    {
        var workTypeLevelFaker = new WorkTypeLevelFaker().GetRandomFromNormalHierarchy();

        RuleFor(x => x.Id, f => f.Random.Number(1, 10000));
        RuleFor(x => x.Name, f => f.Random.String2(10));
        RuleFor(x => x.Description, f => f.Random.String2(10));
        RuleFor(x => x.LevelId, workTypeLevelFaker.Id);
        RuleFor(x => x.Level, workTypeLevelFaker);
        RuleFor(x => x.IsActive, true);
    }
}

public static class WorkTypeFakerExtensions
{
    public static WorkTypeFaker WithId(this WorkTypeFaker faker, int id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static WorkTypeFaker WithName(this WorkTypeFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static WorkTypeFaker WithDescription(this WorkTypeFaker faker, string description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static WorkTypeFaker WithLevel(this WorkTypeFaker faker, WorkTypeLevel level)
    {
        faker.RuleFor(x => x.LevelId, level.Id);
        faker.RuleFor(x => x.Level, level);
        return faker;
    }

    public static WorkTypeFaker WithIsActive(this WorkTypeFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }

    public static WorkTypeFaker AsEpic(this WorkTypeFaker faker)
    {
        var level = new WorkTypeLevelFaker().AsEpics().Generate();

        faker.WithName("Epic");
        faker.WithDescription("The Epic type");
        faker.WithLevel(level);

        return faker;
    }

    public static WorkTypeFaker AsFeature(this WorkTypeFaker faker)
    {
        var level = new WorkTypeLevelFaker().AsFeatures().Generate();

        faker.WithName("Feature");
        faker.WithDescription("The Feature type");
        faker.WithLevel(level);

        return faker;
    }

    public static WorkTypeFaker AsStory(this WorkTypeFaker faker)
    {
        var level = new WorkTypeLevelFaker().AsStories().Generate();

        faker.WithName("Story");
        faker.WithDescription("The Story type");
        faker.WithLevel(level);

        return faker;
    }

    public static WorkTypeFaker AsBug(this WorkTypeFaker faker)
    {
        var level = new WorkTypeLevelFaker().AsStories().Generate();

        faker.WithName("Bug");
        faker.WithDescription("The Bug type");
        faker.WithLevel(level);

        return faker;
    }

    public static WorkTypeFaker AsOther(this WorkTypeFaker faker)
    {
        var level = new WorkTypeLevelFaker().AsOther().Generate();

        faker.WithName("Other");
        faker.WithDescription("The Other type");
        faker.WithLevel(level);

        return faker;
    }

    public static WorkType[] GetNormalWorkTypes(this WorkTypeFaker faker)
    {
        return
        [
            AsEpic(faker).Generate(),
            AsFeature(faker).Generate(),
            AsStory(faker).Generate(),
            AsBug(faker).Generate(),
            AsOther(faker).Generate()
        ];
    }
}
