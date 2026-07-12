using Wayd.Common.Models;
using Wayd.StrategicManagement.Domain.Enums;
using Wayd.StrategicManagement.Domain.Models;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using NodaTime;
using NodaTime.Extensions;
using Wayd.TestData.Core;

namespace Wayd.StrategicManagement.Domain.Tests.Data;

public sealed class VisionFaker : PrivateConstructorFaker<Vision>
{
    public VisionFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Description, f => f.Lorem.Sentence(15));
        RuleFor(x => x.State, f => VisionState.Proposed);
        RuleFor(x => x.Dates, f => null);
    }
}

public static class VisionFakerExtensions
{
    public static VisionFaker WithId(this VisionFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static VisionFaker WithKey(this VisionFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static VisionFaker WithDescription(this VisionFaker faker, string description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static VisionFaker WithState(this VisionFaker faker, VisionState state)
    {
        faker.RuleFor(x => x.State, state);
        return faker;
    }

    public static VisionFaker WithDates(this VisionFaker faker, FlexibleInstantRange? dates)
    {
        faker.RuleFor(x => x.Dates, dates);
        return faker;
    }

    /// <summary>
    /// Generates an active Vision that started 10 days ago.
    /// </summary>
    /// <param name="faker"></param>
    /// <param name="dateTimeProvider"></param>
    /// <returns></returns>
    public static Vision ActiveVision(this VisionFaker faker, TestingDateTimeProvider? dateTimeProvider = null)
    {
        var now = dateTimeProvider?.Now ?? DateTime.UtcNow.ToInstant();
        var defaultActiveInstant = now.Plus(Duration.FromDays(-10));

        return faker
            .WithState(VisionState.Active)
            .WithDates(new FlexibleInstantRange(defaultActiveInstant))
            .Generate();
    }

    /// <summary>
    /// Generates an archived Vision that started 20 days ago and ended 10 days ago.
    /// </summary>
    /// <param name="faker"></param>
    /// <param name="dateTimeProvider"></param>
    /// <returns></returns>
    public static Vision ArchivedVision(this VisionFaker faker, TestingDateTimeProvider? dateTimeProvider = null)
    {
        var now = dateTimeProvider?.Now ?? DateTime.UtcNow.ToInstant();
        var defaultActiveInstant = now.Plus(Duration.FromDays(-20));
        var defaultArchivedInstant = defaultActiveInstant.Plus(Duration.FromDays(10));

        return faker
            .WithState(VisionState.Archived)
            .WithDates(new FlexibleInstantRange(defaultActiveInstant, defaultArchivedInstant))
            .Generate();
    }

    public static VisionFaker AsProposed(this VisionFaker faker)
    {
        faker.RuleFor(x => x.State, VisionState.Proposed);
        faker.RuleFor(x => x.Dates, f => null);
        return faker;
    }

    public static VisionFaker AsActive(this VisionFaker faker)
    {
        faker.RuleFor(x => x.State, VisionState.Active);
        return faker;
    }

    public static VisionFaker AsArchived(this VisionFaker faker)
    {
        faker.RuleFor(x => x.State, VisionState.Archived);
        return faker;
    }
}
