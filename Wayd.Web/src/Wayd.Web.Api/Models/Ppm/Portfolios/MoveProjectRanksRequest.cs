using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;

namespace Wayd.Web.Api.Models.Ppm.Portfolios;

public sealed record MoveProjectRanksRequest
{
    /// <summary>
    /// The projects to (re)position, already in their intended order. Supports multi-select drag.
    /// </summary>
    public List<Guid> ProjectIds { get; set; } = [];

    /// <summary>
    /// The anchor immediately above the batch in the ranking. Must already be ranked. Null when the
    /// batch is dropped at the top of the ranking.
    /// </summary>
    public Guid? AfterProjectId { get; set; }

    /// <summary>
    /// The anchor immediately below the batch in the ranking. Must already be ranked. Null when the
    /// batch is dropped at the bottom of the ranking.
    /// </summary>
    public Guid? BeforeProjectId { get; set; }

    public MoveProjectRanksCommand ToCommand(Guid portfolioId) =>
        new(portfolioId, ProjectIds, AfterProjectId, BeforeProjectId);
}

public sealed class MoveProjectRanksRequestValidator : CustomValidator<MoveProjectRanksRequest>
{
    public MoveProjectRanksRequestValidator()
    {
        RuleFor(r => r.ProjectIds)
            .NotEmpty()
            .WithMessage("At least one project must be supplied.");

        RuleForEach(r => r.ProjectIds).NotEmpty();

        RuleFor(r => r.ProjectIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("The batch contains duplicate projects.");

        RuleFor(r => r)
            .Must(r => r.AfterProjectId is not null || r.BeforeProjectId is not null)
            .WithMessage("At least one anchor must be supplied.");

        RuleFor(r => r)
            .Must(r => r.AfterProjectId is null || !r.ProjectIds.Contains(r.AfterProjectId.Value))
            .WithMessage("An anchor cannot also be in the batch.");

        RuleFor(r => r)
            .Must(r => r.BeforeProjectId is null || !r.ProjectIds.Contains(r.BeforeProjectId.Value))
            .WithMessage("An anchor cannot also be in the batch.");
    }
}
