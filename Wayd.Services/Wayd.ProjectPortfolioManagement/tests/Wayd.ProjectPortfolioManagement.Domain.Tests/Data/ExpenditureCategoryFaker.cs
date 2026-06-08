using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ExpenditureCategoryFaker : PrivateConstructorFaker<ExpenditureCategory>
{
    public ExpenditureCategoryFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Int(1, 1000));
        RuleFor(x => x.Name, f => f.Commerce.Department());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.State, f => f.PickRandom<ExpenditureCategoryState>());
        RuleFor(x => x.IsCapitalizable, f => f.Random.Bool());
        RuleFor(x => x.RequiresDepreciation, (f, x) => x.IsCapitalizable && f.Random.Bool()); // If capitalizable, may require depreciation
        RuleFor(x => x.AccountingCode, f => f.Random.Bool() ? f.Finance.Account() : null);
    }

    public ExpenditureCategoryFaker WithName(string? name)
    {
        RuleFor(x => x.Name, name);

        return this;
    }

    public ExpenditureCategoryFaker WithDescription(string? description)
    {
        RuleFor(x => x.Description, description);

        return this;
    }

    public ExpenditureCategoryFaker WithState(ExpenditureCategoryState? state)
    {
        RuleFor(x => x.State, state);

        return this;
    }

    public ExpenditureCategoryFaker WithIsCapitalizable(bool? isCapitalizable)
    {
        RuleFor(x => x.IsCapitalizable, isCapitalizable);

        return this;
    }

    public ExpenditureCategoryFaker WithRequiresDepreciation(bool? requiresDepreciation)
    {
        RuleFor(x => x.RequiresDepreciation, requiresDepreciation);

        return this;
    }

    public ExpenditureCategoryFaker WithAccountingCode(string? accountingCode)
    {
        RuleFor(x => x.AccountingCode, accountingCode);

        return this;
    }

    public ExpenditureCategory GenerateProposed() => WithState(ExpenditureCategoryState.Proposed).Generate();
    public ExpenditureCategory GenerateActive() => WithState(ExpenditureCategoryState.Active).Generate();
    public ExpenditureCategory GenerateArchived() => WithState(ExpenditureCategoryState.Archived).Generate();
}