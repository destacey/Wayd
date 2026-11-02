using Wayd.ProductManagement.Application.Incidents.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Incidents;

/// <summary>Records a note on an incident's operational timeline.</summary>
public sealed class AddIncidentTimelineNoteRequest
{
    /// <summary>The incident id, repeated from the route.</summary>
    public Guid IncidentId { get; set; }

    /// <summary>What happened or what responders should know.</summary>
    public string Body { get; set; } = default!;

    /// <summary>When the event occurred; defaults to now when omitted.</summary>
    public Instant? RecordedAt { get; set; }

    /// <summary>Builds the application command.</summary>
    public AddIncidentTimelineNoteCommand ToCommand() =>
        new(IncidentId, Body, RecordedAt);
}

public sealed class AddIncidentTimelineNoteRequestValidator : CustomValidator<AddIncidentTimelineNoteRequest>
{
    public AddIncidentTimelineNoteRequestValidator()
    {
        RuleFor(r => r.IncidentId).NotEmpty();
        RuleFor(r => r.Body).NotEmpty().MaximumLength(4096);
    }
}
