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

/// <summary>
/// Stores a pasted Teams link as the incident's one chat, or removes it when the link is empty.
/// Wayd does not join the chat.
/// </summary>
public sealed class LinkIncidentTeamsChatRequest
{
    public Guid Id { get; set; }

    /// <summary>The https link that opens the Teams chat. Empty removes the stored chat.</summary>
    public string? Link { get; set; }

    /// <summary>The name shown beside the link. Empty when the link stands alone.</summary>
    public string? DisplayName { get; set; }

    public LinkIncidentTeamsChatCommand ToCommand() => new(Id, Link, DisplayName);
}

public sealed class LinkIncidentTeamsChatRequestValidator : CustomValidator<LinkIncidentTeamsChatRequest>
{
    public LinkIncidentTeamsChatRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
        RuleFor(r => r.Link)
            .MaximumLength(2048)
            .Must(BeHttpsAbsoluteUrlOrEmpty)
            .WithMessage("A Teams chat link must be an https URL.");
        RuleFor(r => r.DisplayName).MaximumLength(256);
    }

    private static bool BeHttpsAbsoluteUrlOrEmpty(string? link)
    {
        var trimmed = link?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return true;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }
}

/// <summary>
/// Creates the incident's one Teams chat through the active Teams connector.
/// Extra people are included only in that chat and are not stored.
/// </summary>
public sealed class CreateIncidentTeamsChatRequest
{
    public Guid Id { get; set; }

    /// <summary>Employees included in this chat only. They are not stored on the incident.</summary>
    public IReadOnlyList<Guid>? ExtraEmployeeIds { get; set; }

    public CreateIncidentTeamsChatCommand ToCommand() => new(Id, ExtraEmployeeIds);
}

public sealed class CreateIncidentTeamsChatRequestValidator : CustomValidator<CreateIncidentTeamsChatRequest>
{
    public CreateIncidentTeamsChatRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
        RuleForEach(r => r.ExtraEmployeeIds).NotEmpty();
    }
}
