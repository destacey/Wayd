using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectScoreOutputFaker : PrivateConstructorFaker<ProjectScoreOutput>
{
    public ProjectScoreOutputFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ProjectScoreId, f => f.Random.Guid());
        RuleFor(x => x.Token, f => f.Random.AlphaNumeric(4));
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Value, f => f.Random.Decimal(0, 100));
        RuleFor(x => x.IsPrimary, f => false);
        RuleFor(x => x.Order, f => 1);
    }
}

public static class ProjectScoreOutputFakerExtensions
{
    public static ProjectScoreOutputFaker WithId(this ProjectScoreOutputFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProjectScoreOutputFaker WithProjectScoreId(this ProjectScoreOutputFaker faker, Guid projectScoreId)
    {
        faker.RuleFor(x => x.ProjectScoreId, projectScoreId);

        return faker;
    }

    public static ProjectScoreOutputFaker WithToken(this ProjectScoreOutputFaker faker, string? token)
    {
        faker.RuleFor(x => x.Token, token);

        return faker;
    }

    public static ProjectScoreOutputFaker WithName(this ProjectScoreOutputFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ProjectScoreOutputFaker WithValue(this ProjectScoreOutputFaker faker, decimal value)
    {
        faker.RuleFor(x => x.Value, value);

        return faker;
    }

    public static ProjectScoreOutputFaker WithIsPrimary(this ProjectScoreOutputFaker faker, bool isPrimary)
    {
        faker.RuleFor(x => x.IsPrimary, isPrimary);

        return faker;
    }

    public static ProjectScoreOutputFaker WithOrder(this ProjectScoreOutputFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }
}