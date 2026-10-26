using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.DemandManagement.Application.IntakeChannels.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// The summary tier of an intake request: enough to recognise that an ask exists.
/// </summary>
/// <remarks>
/// What lists, searches and counts return. On an open or summary-only desk everyone who can view requests
/// reads it, so it carries nothing from the substance tier.
/// </remarks>
public sealed record IntakeRequestSummaryDto
{
    /// <summary>The request's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The request's number, sequential across the install.</summary>
    public int Key { get; init; }

    /// <summary>What is being asked for, in a line.</summary>
    public required string Title { get; init; }

    /// <summary>Where the request stands in its desk's review workflow, as the status was called when it moved there.</summary>
    public required StatusNavigationDto Status { get; init; }

    /// <summary>The desk the request is reviewed on.</summary>
    public required NavigationDto Desk { get; init; }

    /// <summary>The employee the request is for.</summary>
    public required EmployeeNavigationDto Requester { get; init; }

    /// <summary>Where the request came from.</summary>
    public required IntakeChannelNavigationDto Channel { get; init; }

    /// <summary>The member of the request's desk who owns it, when one is assigned.</summary>
    public EmployeeNavigationDto? Assignee { get; init; }

    /// <summary>When the request has to land and why, when it has a need-by date.</summary>
    public IntakeRequestNeedByDto? NeedBy { get; init; }

    /// <summary>When the request was submitted, from which its age is read.</summary>
    public Instant SubmittedOn { get; init; }

    /// <summary>When the request entered its current status, from which its time in that status is read.</summary>
    public Instant StatusChangedOn { get; init; }
}
