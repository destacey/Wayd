using Wayd.AppIntegration.Application.Connections.Commands.Workday;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record CreateWorkdayConnectionRequest : CreateConnectionRequest
{
    /// <summary>The WSDL URL from Workday's "View API Clients" screen (with or without <c>?wsdl</c>).</summary>
    public required string WsdlUrl { get; set; }

    /// <summary>The Integration System User username.</summary>
    public required string IsuUsername { get; set; }

    /// <summary>The Integration System User password.</summary>
    public required string IsuPassword { get; set; }

    /// <summary>Which Workday worker identifier maps onto <c>Employee.EmployeeNumber</c>.</summary>
    public WorkdayWorkerKey WorkerKey { get; set; } = WorkdayWorkerKey.EmployeeId;

    /// <summary>When true, terminated/inactive workers are also returned by the sync.</summary>
    public bool IncludeInactive { get; set; }

    /// <summary>Which uniquely-indexed Employee field the sync upsert matches on.</summary>
    public EmployeeMatchProperty MatchBy { get; set; } = EmployeeMatchProperty.Email;

    /// <summary>
    /// When true, use Workday's <c>User_ID</c> as the email source when <c>Contact_Data</c> is
    /// missing — provided the User_ID parses as a valid email. Workaround for tenants whose ISU
    /// ISSG doesn't grant Worker Data: Personal Contact Information.
    /// </summary>
    public bool UseUserIdAsEmailFallback { get; set; }

    /// <summary>
    /// When true, sync reads each worker's <c>Preferred_Name_Data</c> in preference to
    /// <c>Legal_Name_Data</c>, falling back to legal per-component when a preferred component is
    /// missing. Default off.
    /// </summary>
    public bool UsePreferredName { get; set; }

    /// <summary>
    /// When true, names that come back from Workday in all-caps are title-cased before storage
    /// (mixed-case input is preserved). Default true — most HRIS sources emit upper-cased legal
    /// names that look inconsistent next to manually-entered records.
    /// </summary>
    public bool NormalizeNameCasing { get; set; } = true;

    public CreateWorkdayConnectionCommand ToCommand()
        => new(Name, Description, WsdlUrl, IsuUsername, IsuPassword, WorkerKey, IncludeInactive, MatchBy, UseUserIdAsEmailFallback, UsePreferredName, NormalizeNameCasing);
}

public sealed class CreateWorkdayConnectionRequestValidator : CustomValidator<CreateWorkdayConnectionRequest>
{
    public CreateWorkdayConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        Include(new CreateConnectionRequestValidator());

        RuleFor(x => x.WsdlUrl).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.IsuUsername).NotEmpty().MaximumLength(256);
        RuleFor(x => x.IsuPassword).NotEmpty().MaximumLength(512);
    }
}
