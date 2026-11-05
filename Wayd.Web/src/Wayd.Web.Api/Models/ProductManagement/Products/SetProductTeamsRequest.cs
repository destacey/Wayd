using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// The whole set of teams and teams of teams linked to a product.
/// </summary>
public sealed record SetProductTeamsRequest
{
    /// <summary>
    /// Every team that works on the product, at most 100. Teams not listed are unlinked; an empty list unlinks
    /// them all. Teams linked to the products beneath it are not affected.
    /// </summary>
    public IReadOnlyCollection<Guid> TeamIds { get; set; } = [];

    public SetProductTeamsCommand ToSetProductTeamsCommand(Guid id) => new(id, TeamIds);
}

public sealed class SetProductTeamsRequestValidator : CustomValidator<SetProductTeamsRequest>
{
    public SetProductTeamsRequestValidator()
    {
        RuleFor(r => r.TeamIds)
            .NotNull()
            .Must(ids => ids.Count <= SetProductTeamsCommandValidator.MaxTeams)
            .WithMessage($"A product can be set to at most {SetProductTeamsCommandValidator.MaxTeams} teams at once.");

        RuleForEach(r => r.TeamIds)
            .NotEmpty();
    }
}
