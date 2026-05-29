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

    /// <summary>When true, the runner uses Workday's transaction log to fetch only changed workers after the first successful sync.</summary>
    public bool IncrementalSyncEnabled { get; set; } = true;

    /// <summary>Which uniquely-indexed Employee field the sync upsert matches on.</summary>
    public EmployeeMatchProperty MatchBy { get; set; } = EmployeeMatchProperty.Email;

    public CreateWorkdayConnectionCommand ToCommand()
        => new(Name, Description, WsdlUrl, IsuUsername, IsuPassword, WorkerKey, IncludeInactive, IncrementalSyncEnabled, MatchBy);
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
