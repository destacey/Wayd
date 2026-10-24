using CSharpFunctionalExtensions;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// The upstream record an intake request arrived from: an enterprise request system, a service desk, a
/// portal.
/// </summary>
/// <remarks>
/// Origin is not evidence. It says where this request was raised, and is fixed when the request is
/// created; things elsewhere that back the ask are evidence, of which a request can have many.
/// </remarks>
public sealed record IntakeRequestOrigin
{
    private IntakeRequestOrigin() { }

    private IntakeRequestOrigin(string sourceSystem, string externalId, string? url)
    {
        SourceSystem = sourceSystem;
        ExternalId = externalId;
        Url = url;
    }

    /// <summary>The system the request was raised in.</summary>
    public string SourceSystem { get; private init; } = default!;

    /// <summary>
    /// The request's id in <see cref="SourceSystem"/>. A source system and external id together identify at
    /// most one intake request.
    /// </summary>
    public string ExternalId { get; private init; } = default!;

    /// <summary>A link to the request in <see cref="SourceSystem"/>, when it has one.</summary>
    public string? Url { get; private init; }

    /// <summary>
    /// Builds an origin, refusing one missing its source system or external id.
    /// </summary>
    public static Result<IntakeRequestOrigin> Create(string? sourceSystem, string? externalId, string? url)
    {
        if (string.IsNullOrWhiteSpace(sourceSystem) || string.IsNullOrWhiteSpace(externalId))
            return Result.Failure<IntakeRequestOrigin>("An origin needs both its source system and its external id.");

        return new IntakeRequestOrigin(
            sourceSystem.Trim(),
            externalId.Trim(),
            string.IsNullOrWhiteSpace(url) ? null : url.Trim());
    }
}
