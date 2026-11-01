using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Incidents.Queries;

namespace Wayd.Web.Api.Models.ProductManagement.Incidents;

public sealed class CreateIncidentRequest
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public Guid SeverityLevelId { get; set; }
    public Instant? NoticedAt { get; set; }
    public Instant? StartedAt { get; set; }
    public Instant? RecoveredAt { get; set; }
    public List<Guid> ProductIds { get; set; } = [];

    public CreateIncidentCommand ToCommand() =>
        new(Title, Description, SeverityLevelId, NoticedAt, StartedAt, RecoveredAt, ProductIds);
}

public sealed class CreateIncidentRequestValidator : CustomValidator<CreateIncidentRequest>
{
    public CreateIncidentRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).MaximumLength(4096);
        RuleFor(r => r.SeverityLevelId).NotEmpty();
    }
}

public sealed class UpdateIncidentDetailsRequest
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public Guid SeverityLevelId { get; set; }
    public Instant NoticedAt { get; set; }
    public Instant? StartedAt { get; set; }
    public Instant? RecoveredAt { get; set; }
    public List<Guid> ProductIds { get; set; } = [];

    public UpdateIncidentDetailsCommand ToCommand() =>
        new(Id, Title, Description, SeverityLevelId, NoticedAt, StartedAt, RecoveredAt, ProductIds);
}

public sealed class UpdateIncidentDetailsRequestValidator : CustomValidator<UpdateIncidentDetailsRequest>
{
    public UpdateIncidentDetailsRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
        RuleFor(r => r.Title).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Description).MaximumLength(4096);
        RuleFor(r => r.SeverityLevelId).NotEmpty();
    }
}
