using Microsoft.Extensions.DependencyInjection;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;
using Wayd.Integrations.Workday;
using Wayd.Integrations.Workday.Soap;

namespace Wayd.Infrastructure.ConnectorModules;

public sealed class WorkdayConnectorModule : IConnectorModule
{
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
        // SOAP fault and short-circuits those. Reading the body in the predicate is cheap — fault bodies
        // are tiny and ReadAsStringAsync buffers, so the client's own later read still sees the content —
        // and guarded, so a read failure falls back to the default classification rather than escaping.
        services.AddHttpClient<WorkdayStaffingClient>()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(3);

                var defaultShouldHandle = options.Retry.ShouldHandle;
                options.Retry.ShouldHandle = async args =>
                {
                    var response = args.Outcome.Result;
                    if (response is { IsSuccessStatusCode: false } && response.Content is not null)
                    {
                        try
                        {
                            var body = await response.Content.ReadAsStringAsync(args.Context.CancellationToken);
                            if (WorkdayStaffingClient.IsNonRetryableFault(body))
                                return false;
                        }
                        catch (OperationCanceledException)
                        {
                            // Genuine cancellation (request aborted / total-timeout) — let it propagate
                            // rather than turning it into a retry decision.
                            throw;
                        }
                        catch
                        {
                            // Reading/classifying the body failed (I/O, disposed content). Don't let that
                            // escape the predicate and corrupt the pipeline outcome — fall through to the
                            // default transient classification below.
                        }
                    }

                    return await defaultShouldHandle(args);
                };
            });
        services.AddScoped<IWorkdayEmployeeSource, WorkdayStaffingService>();
        services.AddScoped<IWorkdayConnectionInitializer, WorkdayConnectionInitializer>();

        services.AddKeyedTransient<IEmployeeSource, WorkdayEmployeeSource>(Connector.Workday);
        services.AddKeyedTransient<IConnectionInitProbe, WorkdayConnectionInitProbe>(Connector.Workday);
        services.AddScoped<ISyncableConnectionDescriptorBuilder, WorkdayConnectionDescriptorBuilder>();
    }
}
