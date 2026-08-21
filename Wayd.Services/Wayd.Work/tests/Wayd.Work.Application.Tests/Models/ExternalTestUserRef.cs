using Wayd.Common.Application.Interfaces.ExternalWork;

namespace Wayd.Work.Application.Tests.Models;

public sealed record ExternalTestUserRef : IExternalUserRef
{
    public required string ExternalId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? Handle { get; init; }

    /// <summary>Builds a reference whose id and address both derive from the address, for the common case.</summary>
    public static ExternalTestUserRef FromEmail(string email, string? externalId = null) =>
        new() { ExternalId = externalId ?? email, Email = email, Handle = email };
}
