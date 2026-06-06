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
    public static ProjectScoreOutputFaker WithData(
        this ProjectScoreOutputFaker faker,
        Guid? id = null,
        Guid? projectScoreId = null,
        string? token = null,
        string? name = null,
        decimal? value = null,
        bool? isPrimary = null,
        int? order = null)
    {
        if (id.HasValue) { faker.RuleFor(x => x.Id, id.Value); }
        if (projectScoreId.HasValue) { faker.RuleFor(x => x.ProjectScoreId, projectScoreId.Value); }
        if (!string.IsNullOrWhiteSpace(token)) { faker.RuleFor(x => x.Token, token); }
        if (!string.IsNullOrWhiteSpace(name)) { faker.RuleFor(x => x.Name, name); }
        if (value.HasValue) { faker.RuleFor(x => x.Value, value.Value); }
        if (isPrimary.HasValue) { faker.RuleFor(x => x.IsPrimary, isPrimary.Value); }
        if (order.HasValue) { faker.RuleFor(x => x.Order, order.Value); }

        return faker;
    }
}
