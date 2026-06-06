using NodaTime;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;
using Wayd.Tests.Shared.Data;

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
    public static ProjectScoreFaker WithData(
        this ProjectScoreFaker faker,
        Guid? id = null,
        Guid? projectId = null,
        Guid? scoringModelId = null,
        int? scoringModelKey = null,
        string? scoringModelName = null,
        decimal? primaryValue = null,
        Instant? scoredOn = null,
        Guid? scoredById = null,
        long? sequence = null,
        IEnumerable<ProjectScoreRating>? ratings = null,
        IEnumerable<ProjectScoreOutput>? outputs = null)
    {
        if (id.HasValue) { faker.RuleFor(x => x.Id, id.Value); }
        if (projectId.HasValue) { faker.RuleFor(x => x.ProjectId, projectId.Value); }
        if (scoringModelId.HasValue) { faker.RuleFor(x => x.ScoringModelId, scoringModelId.Value); }
        if (scoringModelKey.HasValue) { faker.RuleFor(x => x.ScoringModelKey, scoringModelKey.Value); }
        if (!string.IsNullOrWhiteSpace(scoringModelName)) { faker.RuleFor(x => x.ScoringModelName, scoringModelName); }
        if (primaryValue.HasValue) { faker.RuleFor(x => x.PrimaryValue, primaryValue.Value); }
        if (scoredOn.HasValue) { faker.RuleFor(x => x.ScoredOn, scoredOn.Value); }
        if (scoredById.HasValue) { faker.RuleFor(x => x.ScoredById, scoredById.Value); }
        if (sequence.HasValue) { faker.RuleFor(x => x.Sequence, sequence.Value); }
        if (ratings is not null) { faker.RuleFor("_ratings", _ => ratings.ToList()); }
        if (outputs is not null) { faker.RuleFor("_outputs", _ => outputs.ToList()); }

        return faker;
    }
}
