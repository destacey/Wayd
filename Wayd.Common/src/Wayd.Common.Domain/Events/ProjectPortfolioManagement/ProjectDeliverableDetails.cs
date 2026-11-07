using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// The descriptive fields of a project deliverable, as one value, so an edit event can carry what it
/// replaced.
/// </summary>
public sealed record ProjectDeliverableDetails(
    string Name,
    string? Description,
    string? AcceptanceCriteria,
    Guid? OwnerId,
    LocalDate? TargetDate);
