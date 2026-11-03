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
    /// The owners of the new Roadmap: employee ids, which must include the caller's own.
    /// </summary>
    public List<Guid> OwnerIds { get; set; } = default!;

    /// <summary>
    /// The visibility id for the new Roadmap. If the Roadmap is public, all users can see the Roadmap. Otherwise, only its owners and Roadmaps administrators can see it.
    /// </summary>
    public int VisibilityId { get; set; }

    public CopyRoadmapCommand ToCopyRoadmapCommand()
    {
        return new CopyRoadmapCommand(SourceRoadmapId, Name, OwnerIds, (Visibility)VisibilityId);
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

        RuleFor(t => t.OwnerIds)
            .NotEmpty();

        RuleForEach(t => t.OwnerIds)
            .NotEmpty();

        RuleFor(t => (Visibility)t.VisibilityId)
            .IsInEnum()
            .WithMessage("A valid visibility must be selected.");
    }
}
