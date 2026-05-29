namespace Wayd.Common.Application.Interfaces.ExternalPeople;

/// <summary>
/// Runs a small probe against a Workday tenant to validate that the connection can authenticate
/// and that the ISU's security group grants access to the fields Wayd requires. Separate from
/// <see cref="IWorkdayEmployeeSource"/> so init probes and full syncs have different acceptance
/// criteria — both reuse the same underlying SOAP client.
/// </summary>
public interface IWorkdayConnectionInitializer
{
    Task<ConnectionInitResult> Initialize(WorkdayConnectionCredentials credentials, CancellationToken cancellationToken);
}

/// <summary>Structured outcome of an init probe. Persisted on the connection so the UI can render it without re-running.</summary>
public sealed record ConnectionInitResult(
    bool IsValid,
    int WorkersProbed,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<string> Warnings,
    string? AuthError);
