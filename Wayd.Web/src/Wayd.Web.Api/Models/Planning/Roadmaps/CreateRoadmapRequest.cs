using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.Roadmaps.Commands;

namespace Wayd.Web.Api.Models.Planning.Roadmaps;

public sealed record CreateRoadmapRequest
{
    /// <summary>
    /// The name of the Roadmap.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The description of the Roadmap.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The Roadmap start date.
    /// </summary>
    public LocalDate Start { get; set; }

    /// <summary>
    /// The Roadmap end date.
    /// </summary>
    public LocalDate End { get; set; }

    /// <summary>
    /// The owners of the Roadmap: employee ids, which must include the caller's own.
    /// </summary>
    public List<Guid> OwnerIds { get; set; } = default!;

    /// <summary>
    /// The visibility id for the Roadmap. If the Roadmap is public, all users can see the Roadmap. Otherwise, only its owners and Roadmaps administrators can see it.
    /// </summary>
    public int VisibilityId { get; set; }

    public CreateRoadmapCommand ToCreateRoadmapCommand()
    {
        return new CreateRoadmapCommand(Name, Description, new LocalDateRange(Start, End), OwnerIds, (Visibility)VisibilityId);
    }
}

public sealed class CreateRoadmapRequestValidator : CustomValidator<CreateRoadmapRequest>
{
    public CreateRoadmapRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.Description)
            .MaximumLength(2048);

        RuleFor(t => t.Start)
            .NotNull();

        RuleFor(t => t.End)
            .NotNull()
            .Must((roadmap, end) => roadmap.Start <= end)
                .WithMessage("End date must be greater than or equal to start date");

        RuleFor(t => t.OwnerIds)
            .NotEmpty();

        RuleForEach(t => t.OwnerIds)
            .NotEmpty();

        RuleFor(t => (Visibility)t.VisibilityId)
            .IsInEnum()
            .WithMessage("A valid visibility must be selected.");
    }
}
