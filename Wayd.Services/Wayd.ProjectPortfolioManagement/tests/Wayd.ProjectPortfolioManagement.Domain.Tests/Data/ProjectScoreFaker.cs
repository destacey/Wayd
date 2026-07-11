using NodaTime;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectScoreFaker : PrivateConstructorFaker<ProjectScore>
{
    public ProjectScoreFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ProjectId, f => f.Random.Guid());
        RuleFor(x => x.ScoringModelId, f => f.Random.Guid());
        RuleFor(x => x.ScoringModelKey, f => f.Random.Int(1, 10000));
        RuleFor(x => x.ScoringModelName, f => f.Commerce.ProductName());
        RuleFor(x => x.PrimaryValue, f => f.Random.Decimal(0, 100));
        RuleFor(x => x.ScoredOn, f => Instant.FromUtc(2026, 5, 1, 0, 0));
        RuleFor(x => x.ScoredById, f => f.Random.Guid());
        RuleFor(x => x.Sequence, f => 1L);
    }
}

public static class ProjectScoreFakerExtensions
{
    public static ProjectScoreFaker WithId(this ProjectScoreFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProjectScoreFaker WithProjectId(this ProjectScoreFaker faker, Guid projectId)
    {
        faker.RuleFor(x => x.ProjectId, projectId);

        return faker;
    }

    public static ProjectScoreFaker WithScoringModelId(this ProjectScoreFaker faker, Guid scoringModelId)
    {
        faker.RuleFor(x => x.ScoringModelId, scoringModelId);

        return faker;
    }

    public static ProjectScoreFaker WithScoringModelKey(this ProjectScoreFaker faker, int scoringModelKey)
    {
        faker.RuleFor(x => x.ScoringModelKey, scoringModelKey);

        return faker;
    }

    public static ProjectScoreFaker WithScoringModelName(this ProjectScoreFaker faker, string? scoringModelName)
    {
        faker.RuleFor(x => x.ScoringModelName, scoringModelName);

        return faker;
    }

    public static ProjectScoreFaker WithPrimaryValue(this ProjectScoreFaker faker, decimal primaryValue)
    {
        faker.RuleFor(x => x.PrimaryValue, primaryValue);

        return faker;
    }

    public static ProjectScoreFaker WithScoredOn(this ProjectScoreFaker faker, Instant scoredOn)
    {
        faker.RuleFor(x => x.ScoredOn, scoredOn);

        return faker;
    }

    public static ProjectScoreFaker WithScoredById(this ProjectScoreFaker faker, Guid? scoredById)
    {
        faker.RuleFor(x => x.ScoredById, scoredById);

        return faker;
    }

    public static ProjectScoreFaker WithSequence(this ProjectScoreFaker faker, long sequence)
    {
        faker.RuleFor(x => x.Sequence, sequence);

        return faker;
    }

    public static ProjectScoreFaker WithRatings(this ProjectScoreFaker faker, IEnumerable<ProjectScoreRating> ratings)
    {
        faker.RuleFor("_ratings", _ => ratings.ToList());

        return faker;
    }

    public static ProjectScoreFaker WithOutputs(this ProjectScoreFaker faker, IEnumerable<ProjectScoreOutput> outputs)
    {
        faker.RuleFor("_outputs", _ => outputs.ToList());

        return faker;
    }
}
