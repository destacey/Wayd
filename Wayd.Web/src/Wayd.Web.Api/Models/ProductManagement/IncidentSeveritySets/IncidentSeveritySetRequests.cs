using Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.IncidentSeveritySets;

/// <summary>
/// One level in a severity set payload.
/// </summary>
public sealed class IncidentSeveritySetLevelRequest
{
    /// <summary>Existing level id, or omitted when adding a level.</summary>
    public Guid? Id { get; set; }

    public string Name { get; set; } = default!;
}

public sealed class CreateIncidentSeveritySetRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<string> LevelNames { get; set; } = [];

    public CreateIncidentSeveritySetCommand ToCommand() =>
        new(Name, Description, LevelNames);
}

public sealed class CreateIncidentSeveritySetRequestValidator : CustomValidator<CreateIncidentSeveritySetRequest>
{
    public CreateIncidentSeveritySetRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
        RuleFor(r => r.LevelNames).NotEmpty().Must(v => v.Count >= 2);
        RuleForEach(r => r.LevelNames).NotEmpty().MaximumLength(64);
    }
}

public sealed class UpdateIncidentSeveritySetRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<IncidentSeveritySetLevelRequest> Levels { get; set; } = [];

    public UpdateIncidentSeveritySetCommand ToCommand() =>
        new(
            Id,
            Name,
            Description,
            Levels.Select(l => new IncidentSeveritySetLevelPayload(l.Id, l.Name)).ToList());
}

public sealed class UpdateIncidentSeveritySetRequestValidator : CustomValidator<UpdateIncidentSeveritySetRequest>
{
    public UpdateIncidentSeveritySetRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
        RuleFor(r => r.Levels).NotEmpty().Must(v => v.Count >= 2);
        RuleForEach(r => r.Levels).ChildRules(level =>
        {
            level.RuleFor(l => l.Name).NotEmpty().MaximumLength(64);
        });
    }
}

public sealed class SetIncidentSeveritySetActiveStatusRequest
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }

    public SetIncidentSeveritySetActiveStatusCommand ToCommand() => new(Id, IsActive);
}

public sealed class SelectIncidentSeveritySetRequest
{
    public Guid SeveritySetId { get; set; }

    public SelectIncidentSeveritySetCommand ToCommand() => new(SeveritySetId);
}

public sealed class SelectIncidentSeveritySetRequestValidator : CustomValidator<SelectIncidentSeveritySetRequest>
{
    public SelectIncidentSeveritySetRequestValidator()
    {
        RuleFor(r => r.SeveritySetId).NotEmpty();
    }
}
