using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// The whole set of products a team or team of teams is linked to.
/// </summary>
public sealed record SetTeamProductsRequest
{
    /// <summary>
    /// Every product the team works on, at most 500. Products not listed are unlinked; an empty list unlinks the
    /// team from every product.
    /// </summary>
    public IReadOnlyCollection<Guid> ProductIds { get; set; } = [];

    public SetTeamProductsCommand ToSetTeamProductsCommand(Guid teamId) => new(teamId, ProductIds);
}

public sealed class SetTeamProductsRequestValidator : CustomValidator<SetTeamProductsRequest>
{
    public SetTeamProductsRequestValidator()
    {
        RuleFor(r => r.ProductIds)
            .NotNull()
            .Must(ids => ids.Count <= SetTeamProductsCommandValidator.MaxProducts)
            .WithMessage($"A team can be set to at most {SetTeamProductsCommandValidator.MaxProducts} products at once.");

        RuleForEach(r => r.ProductIds)
            .NotEmpty();
    }
}
