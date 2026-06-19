using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.Roadmaps.Commands;

namespace Wayd.Web.Api.Models.Planning.Roadmaps;

public sealed record CopyRoadmapRequest
{
    /// <summary>
    /// The Id of the source Roadmap to copy.
    /// </summary>
    public Guid SourceRoadmapId { get; set; }

    /// <summary>
    /// The name of the new Roadmap.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The managers of the new Roadmap.
    /// </summary>
    public List<Guid> RoadmapManagerIds { get; set; } = default!;

    /// <summary>
    /// The visibility id for the new Roadmap. If the Roadmap is public, all users can see the Roadmap. Otherwise, only the Roadmap Managers can see the Roadmap.
    /// </summary>
    public int VisibilityId { get; set; }

    public CopyRoadmapCommand ToCopyRoadmapCommand()
    {
        return new CopyRoadmapCommand(SourceRoadmapId, Name, RoadmapManagerIds, (Visibility)VisibilityId);
    }
}

public sealed class CopyRoadmapRequestValidator : CustomValidator<CopyRoadmapRequest>
{
    public CopyRoadmapRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.SourceRoadmapId)
            .NotEmpty();

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.RoadmapManagerIds)
            .NotEmpty();

        RuleForEach(t => t.RoadmapManagerIds)
            .NotEmpty();

        RuleFor(t => (Visibility)t.VisibilityId)
            .IsInEnum()
            .WithMessage("A valid visibility must be selected.");
    }
}
