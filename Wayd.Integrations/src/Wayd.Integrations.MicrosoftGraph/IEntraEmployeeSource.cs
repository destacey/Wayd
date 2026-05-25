using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;

namespace Wayd.Integrations.MicrosoftGraph;

/// <summary>
/// Fetches employees from a specific Entra (Microsoft Graph) tenant identified by the supplied
/// <see cref="EntraConnectionCredentials"/>. Each call builds its own <c>GraphServiceClient</c>
/// — there is no shared per-process tenant configuration.
/// </summary>
public interface IEntraEmployeeSource
{
    Task<Result<IEnumerable<IExternalEmployee>>> GetEmployees(EntraConnectionCredentials credentials, CancellationToken cancellationToken);
}
