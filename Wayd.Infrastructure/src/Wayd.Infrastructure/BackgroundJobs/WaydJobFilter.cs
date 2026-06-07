using System.Security.Claims;
using Hangfire.Client;
using Hangfire.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Identity;

namespace Wayd.Infrastructure.BackgroundJobs;

public class WaydJobFilter : IClientFilter
{
    private static readonly ILog _logger = LogProvider.GetCurrentClassLogger();
    private const string JobAdminUserId = SystemIdentity.UserId;

    private readonly IServiceProvider _services;

    public WaydJobFilter(IServiceProvider services) => _services = services;

    public void OnCreating(CreatingContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        _logger.InfoFormat("Set UserId parameters to job {0}.{1}...", context.Job.Method.ReflectedType?.FullName, context.Job.Method.Name);

        using var scope = _services.CreateScope();

        var httpContext = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>()?.HttpContext;
        //_ = httpContext ?? throw new InvalidOperationException("Can't create a Job without HttpContext.");

        string? userId = httpContext?.User.GetUserId() ?? JobAdminUserId;
        context.SetJobParameter(QueryStringKeys.UserId, userId);
    }

    public void OnCreated(CreatedContext context) =>
        _logger.InfoFormat(
            "Job created with parameters {0}",
            context.Parameters.Select(x => x.Key + "=" + x.Value).Aggregate((s1, s2) => s1 + ";" + s2));
}