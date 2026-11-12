using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Incidents.Queries;

namespace Wayd.Web.Api.Models.ProductManagement.Incidents;

public sealed class CreateIncidentRequest
{
    public string Title { get; set; } = default!;

    /// <summary>What responders are seeing. Empty when nothing has been recorded yet.</summary>
    public string? Symptoms { get; set; }

    /// <summary>What was done to restore service or stop the impact. Empty until a remediation is recorded.</summary>
    public string? Remediation { get; set; }

    /// <summary>Why it happened, once that is known. Empty until a cause is recorded.</summary>
    public string? RootCause { get; set; }

    public Guid SeverityLevelId { get; set; }

    /// <summary>The employee who reported the incident. Required, and not inferred from who opened it.</summary>
    public Guid ReportedByEmployeeId { get; set; }

    /// <summary>The employee commanding the response. Empty when nobody is assigned.</summary>
    public Guid? CommanderEmployeeId { get; set; }

    public Instant? NoticedAt { get; set; }
    public Instant? StartedAt { get; set; }
    public Instant? RecoveredAt { get; set; }
    public List<Guid> ProductIds { get; set; } = [];

    /// <summary>Releases this incident points at. One incident may point at several.</summary>
    public List<Guid> ReleaseIds { get; set; } = [];

    public CreateIncidentCommand ToCommand() =>
        new(Title, Symptoms, Remediation, RootCause, SeverityLevelId, NoticedAt, StartedAt, RecoveredAt, ProductIds, ReportedByEmployeeId, CommanderEmployeeId, ReleaseIds);
}

public sealed class CreateIncidentRequestValidator : CustomValidator<CreateIncidentRequest>
{
    public CreateIncidentRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Symptoms).MaximumLength(4096);
        RuleFor(r => r.Remediation).MaximumLength(4096);
        RuleFor(r => r.RootCause).MaximumLength(4096);
        RuleFor(r => r.SeverityLevelId).NotEmpty();
        RuleFor(r => r.ReportedByEmployeeId).NotEmpty();
    }
}

public sealed class UpdateIncidentDetailsRequest
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;

    /// <summary>What responders are seeing. Empty when nothing has been recorded yet.</summary>
    public string? Symptoms { get; set; }

    /// <summary>What was done to restore service or stop the impact. Empty until a remediation is recorded.</summary>
    public string? Remediation { get; set; }

    /// <summary>Why it happened, once that is known. Empty until a cause is recorded.</summary>
    public string? RootCause { get; set; }
    public Guid SeverityLevelId { get; set; }

    /// <summary>The employee who reported the incident.</summary>
    public Guid ReportedByEmployeeId { get; set; }

    /// <summary>The employee commanding the response. Empty when nobody is assigned.</summary>
    public Guid? CommanderEmployeeId { get; set; }

    public Instant NoticedAt { get; set; }
    public Instant? StartedAt { get; set; }
    public Instant? RecoveredAt { get; set; }
    public List<Guid> ProductIds { get; set; } = [];

    /// <summary>Releases this incident points at. One incident may point at several.</summary>
    public List<Guid> ReleaseIds { get; set; } = [];

    public UpdateIncidentDetailsCommand ToCommand() =>
        new(Id, Title, Symptoms, Remediation, RootCause, SeverityLevelId, NoticedAt, StartedAt, RecoveredAt, ProductIds, ReportedByEmployeeId, CommanderEmployeeId, ReleaseIds);
}

public sealed class UpdateIncidentDetailsRequestValidator : CustomValidator<UpdateIncidentDetailsRequest>
{
    public UpdateIncidentDetailsRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
        RuleFor(r => r.Title).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Symptoms).MaximumLength(4096);
        RuleFor(r => r.Remediation).MaximumLength(4096);
        RuleFor(r => r.RootCause).MaximumLength(4096);
        RuleFor(r => r.SeverityLevelId).NotEmpty();
        RuleFor(r => r.ReportedByEmployeeId).NotEmpty();
    }
}
