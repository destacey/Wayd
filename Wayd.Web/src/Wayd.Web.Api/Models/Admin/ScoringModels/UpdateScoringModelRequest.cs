using Wayd.Common.Application.Scoring.ScoringModels.Commands;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record UpdateScoringModelRequest
{
    /// <summary>
    /// The name of the scoring model.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The description of the scoring model.
    /// </summary>
    public string Description { get; set; } = default!;

    public UpdateScoringModelCommand ToUpdateScoringModelCommand(Guid id)
    {
        return new UpdateScoringModelCommand(id, Name, Description);
    }
}

public sealed class UpdateScoringModelRequestValidator : AbstractValidator<UpdateScoringModelRequest>
{
    public UpdateScoringModelRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}
