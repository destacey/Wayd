namespace Wayd.Common.Application.Interfaces.ExternalPeople;

/// <summary>
/// Fetches workers from a specific Workday tenant identified by the supplied
/// <see cref="WorkdayConnectionCredentials"/>. Each call builds its own SOAP client — there is no
/// shared per-process tenant configuration.
/// </summary>
public interface IWorkdayEmployeeSource
{
    Task<Result<IEnumerable<IExternalEmployee>>> GetEmployees(WorkdayConnectionCredentials credentials, CancellationToken cancellationToken);
}
