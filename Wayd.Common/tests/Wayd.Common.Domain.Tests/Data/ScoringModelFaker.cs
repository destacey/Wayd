using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class ScoringModelFaker : PrivateConstructorFaker<ScoringModel>
{
    public ScoringModelFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.State, f => ScoringModelState.Proposed);
    }
}

public static class ScoringModelFakerExtensions
{
    public static ScoringModelFaker WithId(this ScoringModelFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static ScoringModelFaker WithKey(this ScoringModelFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static ScoringModelFaker WithName(this ScoringModelFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static ScoringModelFaker WithDescription(this ScoringModelFaker faker, string description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static ScoringModelFaker WithState(this ScoringModelFaker faker, ScoringModelState state)
    {
        faker.RuleFor(x => x.State, state);
        return faker;
    }

    /// <summary>
    /// Generates a proposed model with the specified scales, criteria, and outputs, built via the
    /// aggregate's own methods so invariants and ordering hold. Criteria reference a scale by name.
    /// </summary>
    public static ScoringModel AsProposedWith(
        this ScoringModelFaker faker,
        (string Name, (string Label, decimal Value)[] Levels)[] scales,
        (string Name, string Token, decimal? Weight, string? ScaleName)[] criteria,
        (string Name, string Token, string Formula, bool IsPrimary)[] outputs)
    {
        var model = faker.Generate();
        return model
            .WithScales(scales)
            .WithCriteria(criteria)
            .WithOutputs(outputs);
    }

    /// <summary>
    /// Generates an active model with the specified scales, criteria, and outputs.
    /// </summary>
    public static ScoringModel AsActiveWith(
        this ScoringModelFaker faker,
        (string Name, (string Label, decimal Value)[] Levels)[] scales,
        (string Name, string Token, decimal? Weight, string? ScaleName)[] criteria,
        (string Name, string Token, string Formula, bool IsPrimary)[] outputs)
    {
        var model = faker.AsProposedWith(scales, criteria, outputs);
        model.Activate();
        return model;
    }

    // The canonical WSJF fixture: an "Impact" scale, the four WSJF criteria rated on it, an intermediate
    // Cost of Delay output, and the primary WSJF output. The conventional worked example for this domain.
    private static readonly (string Name, (string Label, decimal Value)[] Levels)[] WsjfScales =
        [("Impact", [("High", 8m), ("Medium", 5m), ("Low", 1m)])];

    private static readonly (string Name, string Token, decimal? Weight, string? ScaleName)[] WsjfCriteria =
    [
        ("Business Value", "BV", null, "Impact"),
        ("Time Criticality", "TC", null, "Impact"),
        ("Risk Reduction", "RR", null, "Impact"),
        ("Job Size", "JS", null, "Impact")
    ];

    private static readonly (string Name, string Token, string Formula, bool IsPrimary)[] WsjfOutputs =
    [
        ("Cost of Delay", "CoD", "BV + TC + RR", false),
        ("WSJF", "WSJF", "CoD / JS", true)
    ];

    /// <summary>
    /// Generates a complete, valid WSJF model in the Proposed state — scale, four criteria, and the
    /// CoD/WSJF output chain — so tests that just need "a realistic model" don't repeat the fixture.
    /// </summary>
    public static ScoringModel AsProposedWsjf(this ScoringModelFaker faker)
        => faker.AsProposedWith(WsjfScales, WsjfCriteria, WsjfOutputs);

    /// <summary>
    /// Generates the same WSJF model as <see cref="AsProposedWsjf"/>, activated.
    /// </summary>
    public static ScoringModel AsActiveWsjf(this ScoringModelFaker faker)
        => faker.AsActiveWith(WsjfScales, WsjfCriteria, WsjfOutputs);

    /// <summary>
    /// Adds scales (and their levels) to an existing model using the model's own methods.
    /// </summary>
    public static ScoringModel WithScales(
        this ScoringModel model,
        params (string Name, (string Label, decimal Value)[] Levels)[] scales)
    {
        foreach (var (name, levels) in scales)
        {
            var scale = model.AddScale(name).Value;
            foreach (var (label, value) in levels)
            {
                model.AddScaleLevel(scale.Id, label, value);
            }
        }
        return model;
    }

    /// <summary>
    /// Adds criteria to an existing model using the model's AddCriterion method, resolving a referenced
    /// scale by name.
    /// </summary>
    public static ScoringModel WithCriteria(
        this ScoringModel model,
        params (string Name, string Token, decimal? Weight, string? ScaleName)[] criteria)
    {
        foreach (var (name, token, weight, scaleName) in criteria)
        {
            Guid? scaleId = scaleName is null
                ? null
                : model.Scales.FirstOrDefault(s => s.Name == scaleName)?.Id;
            model.AddCriterion(name, token, null, weight, scaleId);
        }
        return model;
    }

    /// <summary>
    /// Adds outputs to an existing model using the model's AddOutput method.
    /// </summary>
    public static ScoringModel WithOutputs(
        this ScoringModel model,
        params (string Name, string Token, string Formula, bool IsPrimary)[] outputs)
    {
        foreach (var (name, token, formula, isPrimary) in outputs)
        {
            model.AddOutput(name, token, formula, isPrimary);
        }
        return model;
    }
}
