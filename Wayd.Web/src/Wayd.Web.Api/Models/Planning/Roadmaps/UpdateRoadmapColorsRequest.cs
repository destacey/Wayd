using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Web.Api.Models.Planning.Roadmaps;

public sealed record UpdateRoadmapColorsRequest
{
    /// <summary>
    /// The unique identifier of the Roadmap.
    /// </summary>
    public Guid RoadmapId { get; set; }

    /// <summary>
    /// The colors to configure on the Roadmap. The provided set fully replaces the existing colors.
    /// </summary>
    public List<UpsertRoadmapColorRequest> Colors { get; set; } = default!;

    public UpdateRoadmapColorsCommand ToUpdateRoadmapColorsCommand()
    {
        return new UpdateRoadmapColorsCommand(
            RoadmapId,
            Colors.Select(c => new UpsertRoadmapColorModel(c.Color, c.Name, c.Order, c.IsDefault)).ToList());
    }
}

public sealed record UpsertRoadmapColorRequest
{
    /// <summary>
    /// The color, as a hex code (e.g. "#4096FF").
    /// </summary>
    public string Color { get; set; } = default!;

    /// <summary>
    /// The caption describing what the color represents on this Roadmap.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The order of the color within the Roadmap's configured colors.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether this is the default color applied to activities that have no color of their own.
    /// </summary>
    public bool IsDefault { get; set; }
}

public sealed class UpdateRoadmapColorsRequestValidator : CustomValidator<UpdateRoadmapColorsRequest>
{
    public UpdateRoadmapColorsRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.RoadmapId)
            .NotEmpty();

        RuleFor(t => t.Colors)
            .NotNull()
            .Must(colors => colors.Count <= Roadmap.MaxColors)
                .WithMessage($"A Roadmap cannot have more than {Roadmap.MaxColors} colors.")
            .Must(colors => colors.Count(c => c.IsDefault) <= 1)
                .WithMessage("Only one color can be marked as the default.");

        RuleForEach(t => t.Colors).ChildRules(color =>
        {
            color.RuleFor(c => c.Color)
                .NotEmpty()
                .Matches("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")
                    .WithMessage("Color must be a valid hex color code.");

            color.RuleFor(c => c.Name)
                .NotEmpty()
                .MaximumLength(32);
        });
    }
}
