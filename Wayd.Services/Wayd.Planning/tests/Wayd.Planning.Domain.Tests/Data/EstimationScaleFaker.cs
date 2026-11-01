using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

/// <summary>
/// Builds an estimation scale with a single published version, as one created through the domain would be.
/// </summary>
public class EstimationScaleFaker : PrivateConstructorFaker<EstimationScale>
{
    /// <summary>
    /// The values a <see cref="EstimationScaleFakerExtensions.WithValues"/> call treats as cards rather than estimates.
    /// </summary>
    public static readonly string[] Cards = ["?", "∞", "☕"];

    internal string[] Values { get; set; } = ["1", "2", "3", "5", "8", "13", "?"];

    internal Instant PublishedOn { get; set; } = Instant.FromUtc(2026, 1, 1, 0, 0);

    public EstimationScaleFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Lorem.Word());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.IsActive, true);
        RuleFor("_versions", (f, scale) => new List<EstimationScaleVersion>
        {
            new(scale.Id, 1, Values.Select(v => new EstimationScaleValue(v, !Cards.Contains(v))), PublishedOn),
        });
    }
}

public static class EstimationScaleFakerExtensions
{
    public static EstimationScaleFaker WithId(this EstimationScaleFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static EstimationScaleFaker WithKey(this EstimationScaleFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static EstimationScaleFaker WithName(this EstimationScaleFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static EstimationScaleFaker WithDescription(this EstimationScaleFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    /// <summary>
    /// The values of the scale's version 1, in order. <see cref="EstimationScaleFaker.Cards"/> are flagged as cards,
    /// every other value as an estimate.
    /// </summary>
    public static EstimationScaleFaker WithValues(this EstimationScaleFaker faker, params string[] values)
    {
        faker.Values = values;
        return faker;
    }

    public static EstimationScaleFaker WithIsActive(this EstimationScaleFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
