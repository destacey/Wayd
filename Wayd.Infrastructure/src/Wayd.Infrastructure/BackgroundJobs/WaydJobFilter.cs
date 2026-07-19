using System.Security.Claims;
using Hangfire.Client;
using Hangfire.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Wayd.Infrastructure.BackgroundJobs;

public class WaydJobFilter : IClientFilter
{
    private static readonly ILog _logger = LogProvider.GetCurrentClassLogger();

    private readonly IServiceProvider _services;

    public WaydJobFilter(IServiceProvider services) => _services = services;

    public void OnCreating(CreatingContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        _logger.InfoFormat("Set UserId parameters to job {0}.{1}...", context.Job.Method.ReflectedType?.FullName, context.Job.Method.Name);

        using var scope = _services.CreateScope();

        // Stamp only a REAL acting user. A job created with no authenticated HTTP user carries no
        // UserId parameter, and its execution scope (no HttpContext, no seeded user) resolves to
        // ActorKind.System — the "nobody = system" rule lives in CurrentUser.Kind, not here.
        // HangfireService.EnqueueSystem still overrides the parameter afterwards for jobs that must
        // run as the system even when a signed-in admin triggered them.
        var httpContext = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext?.User.GetUserId() is { Length: > 0 } userId)
        {
            context.SetJobParameter(QueryStringKeys.UserId, userId);
        }
    }

    public void OnCreated(CreatedContext context) =>
        _logger.InfoFormat(
            "Job created with parameters {0}",
            context.Parameters.Select(x => x.Key + "=" + x.Value).Aggregate((s1, s2) => s1 + ";" + s2));
}
