using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Infrastructure.Networking;
using Wayd.Integrations.Abstractions;
using Wayd.Integrations.Workday;
using Wayd.Integrations.Workday.Soap;

namespace Wayd.Infrastructure.ConnectorModules;

public sealed class WorkdayConnectorModule : IConnectorModule
{
    /// <summary>How long establishing a connection to Workday may take.</summary>
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(15);

    /// <summary>How long the retry check may spend reading a failed response's body.</summary>
    private static readonly TimeSpan _faultReadTimeout = TimeSpan.FromSeconds(30);

    public Connector Connector => Connector.Workday;

    public IReadOnlyList<ConnectorCapability> Capabilities { get; } = [ConnectorCapability.People];

    public void Register(IServiceCollection services)
    {
        // Workday: shared SOAP client used by both bulk sync and the init probe. The runner
        // resolves the keyed IEmployeeSource; Create/Update/Init handlers resolve
        // IWorkdayConnectionInitializer (the controller dispatches via the keyed IConnectionInitProbe).
        //
        // A per-client standard resilience handler REPLACES the global default for this client, so we
        // re-apply the longer integration timeouts here (90s/attempt) and additionally suppress retries
        // for Workday's permanent faults. Workday wraps auth/validation faults in HTTP 500 bodies, which
        // the default transient predicate would retry 3× to no effect; IsNonRetryableFault parses the
        // SOAP fault and short-circuits those (IsPermanentFailure).
        //
        // The endpoint is typed by an administrator and carries the ISU password, so the primary handler connects
        // only to public addresses and follows no redirect (PublicNetwork). As the primary handler, its check runs
        // after service discovery has rewritten the request.
        services.AddHttpClient<WorkdayStaffingClient>()
            .ConfigurePrimaryHttpMessageHandler(sp => PublicNetwork.CreateHandler(
                _connectTimeout,
                proxy: sp.GetRequiredService<IOptions<OutboundProxyOptions>>().Value.Address))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(3);

                var defaultShouldHandle = options.Retry.ShouldHandle;
                options.Retry.ShouldHandle = async args =>
                {
                    if (DestinationRefusedException.IsIn(args.Outcome.Exception))
                        return false;

                    if (args.Outcome.Result is { IsSuccessStatusCode: false } response
                        && await IsPermanentFailure(response, args.Context.CancellationToken))
                        return false;

                    return await defaultShouldHandle(args);
                };
            });
        services.AddScoped<IWorkdayEmployeeSource, WorkdayStaffingService>();
        services.AddScoped<IWorkdayConnectionInitializer, WorkdayConnectionInitializer>();

        services.AddKeyedTransient<IEmployeeSource, WorkdayEmployeeSource>(Connector.Workday);
        services.AddKeyedTransient<IConnectionInitProbe, WorkdayConnectionInitProbe>(Connector.Workday);
        services.AddScoped<ISyncableConnectionDescriptorBuilder, WorkdayConnectionDescriptorBuilder>();
    }

    /// <summary>
    /// Whether a failed Workday response is one a retry cannot fix: a permanent SOAP fault (bad credentials,
    /// an invalid request), or a body too slow to read, which the next attempt would only stream again. A body
    /// larger than <see cref="WorkdayStaffingClient.MaxFaultResponseBytes"/> holds no fault the client could read,
    /// so it is left, like every response false is returned for, to the default transient classification.
    /// </summary>
    /// <remarks>
    /// The body is buffered, within the client's cap on a failed response
    /// (<see cref="WorkdayStaffingClient.MaxFaultResponseBytes"/>), so the client's own later read still sees it. A
    /// read given up part-way leaves the stream unusable, so its reason is recorded on the request
    /// (<see cref="WorkdayStaffingClient.UnreadBodyReason"/>) for the client to report.
    /// </remarks>
    internal static async Task<bool> IsPermanentFailure(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > WorkdayStaffingClient.MaxFaultResponseBytes)
            return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_faultReadTimeout);
        try
        {
            await response.Content.LoadIntoBufferAsync(WorkdayStaffingClient.MaxFaultResponseBytes, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            return WorkdayStaffingClient.IsNonRetryableFault(body);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded)
        {
            response.RequestMessage?.Options.Set(WorkdayStaffingClient.UnreadBodyReason,
                WorkdayStaffingClient.NoFaultMessage((int)response.StatusCode));
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            response.RequestMessage?.Options.Set(WorkdayStaffingClient.UnreadBodyReason,
                $"Workday's error response did not arrive in full within {_faultReadTimeout.TotalSeconds:F0} seconds.");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An I/O or disposed-content failure must not escape the predicate and corrupt the pipeline's outcome.
            return false;
        }
    }
}
