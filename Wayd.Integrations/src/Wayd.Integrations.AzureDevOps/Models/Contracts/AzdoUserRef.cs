using Wayd.Common.Application.Interfaces.ExternalWork;

namespace Wayd.Integrations.AzureDevOps.Models.Contracts;

/// <summary>
/// An Azure DevOps identity as referenced on a work item. <see cref="ExternalId"/> carries the
/// AzDO identity GUID, which survives the address changes that broke email-only matching.
/// </summary>
public sealed record AzdoUserRef : IExternalUserRef
{
    public required string ExternalId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? Handle { get; init; }
}
