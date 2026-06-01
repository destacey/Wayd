using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Interfaces.ExternalPeople;

namespace Wayd.AppIntegration.Application.Connections.Commands.Workday;

/// <summary>
/// Re-runs the init probe against an existing Workday connection. Triggered by the admin from the
/// connection detail page ("Test Connection" button). Updates the connection's
/// <see cref="Wayd.AppIntegration.Domain.Models.Workday.WorkdayConnectionConfiguration.LastInit*"/>
/// fields and <see cref="Wayd.AppIntegration.Domain.Models.Connection.IsValidConfiguration"/>.
/// </summary>
public sealed record InitWorkdayConnectionCommand(Guid Id) : ICommand<ConnectionInitResult>;

internal sealed class InitWorkdayConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IWorkdayConnectionInitializer initializer,
    ILogger<InitWorkdayConnectionCommandHandler> logger) : ICommandHandler<InitWorkdayConnectionCommand, ConnectionInitResult>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IWorkdayConnectionInitializer _initializer = initializer;
    private readonly ILogger<InitWorkdayConnectionCommandHandler> _logger = logger;

    public async Task<Result<ConnectionInitResult>> Handle(InitWorkdayConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.WorkdayConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
                return Result.Failure<ConnectionInitResult>($"Workday connection {request.Id} not found.");

            var credentials = new WorkdayConnectionCredentials(
                connection.Configuration.SoapEndpoint,
                connection.Configuration.TenantAlias,
                connection.Configuration.WsdlVersion,
                connection.Configuration.IsuUsername,
                connection.Configuration.IsuPassword,
                connection.Configuration.WorkerKey,
                connection.Configuration.IncludeInactive,
                IncrementalUpdatedFrom: null,
                UseUserIdAsEmailFallback: connection.Configuration.UseUserIdAsEmailFallback,
                UsePreferredName: connection.Configuration.UsePreferredName,
                NormalizeNameCasing: connection.Configuration.NormalizeNameCasing,
                DepartmentOrganizationTypeId: connection.Configuration.DepartmentOrganizationTypeId);

            var result = await _initializer.Initialize(credentials, cancellationToken);

            connection.RecordInitResult(
                result.IsValid,
                result.MissingRequiredFields,
                result.Warnings,
                result.AuthError,
                result.DiscoveredOrgTypes?.Select(d => new WorkdayOrgType(d.TypeId, d.DisplayName, d.Count)).ToList(),
                DateTimeOffset.UtcNow);

            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {Id}", nameof(InitWorkdayConnectionCommandHandler), request.Id);
            return Result.Failure<ConnectionInitResult>($"Failed to initialize Workday connection {request.Id}: {ex.Message}");
        }
    }
}
