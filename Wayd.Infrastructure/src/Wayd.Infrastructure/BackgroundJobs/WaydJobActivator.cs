using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Context;

namespace Wayd.Infrastructure.BackgroundJobs;

public class WaydJobActivator : JobActivator
{
    private readonly IServiceScopeFactory _scopeFactory;

    public WaydJobActivator(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    public override JobActivatorScope BeginScope(PerformContext context) =>
        new Scope(context, _scopeFactory.CreateScope());

    private class Scope : JobActivatorScope, IServiceProvider
    {
        private readonly PerformContext _context;
        private readonly IServiceScope _scope;

        // Serilog LogContext properties pushed for the lifetime of the job, disposed with the scope.
        private readonly List<IDisposable> _logContextProperties = [];

        public Scope(PerformContext context, IServiceScope scope)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            ReceiveParameters();
            EnrichLogContext();
        }

        private void ReceiveParameters()
        {
            string userId = _context.GetJobParameter<string>(QueryStringKeys.UserId);
            if (!string.IsNullOrEmpty(userId))
            {
                _scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>()
                    .SetCurrentUserId(userId);
            }
        }

        /// <summary>
        /// Pushes the acting user onto the Serilog <see cref="LogContext"/> so every log event emitted
        /// during the job carries it — mirroring what <c>RequestLoggingMiddleware</c> does for HTTP
        /// requests. Without this, background-job log events have no UserId/UserEmail (that enrichment is
        /// otherwise HTTP-only). Resolved after <see cref="ReceiveParameters"/> so the id is already set.
        /// </summary>
        private void EnrichLogContext()
        {
            var currentUser = _scope.ServiceProvider.GetRequiredService<ICurrentUser>();
            var userId = currentUser.GetUserId();
            var userEmail = currentUser.GetUserEmail();

            _logContextProperties.Add(LogContext.PushProperty("UserId", userId));
            _logContextProperties.Add(LogContext.PushProperty(
                "UserEmail",
                !string.IsNullOrEmpty(userEmail) ? userEmail : "System"));
        }

        public override object Resolve(Type type) =>
            ActivatorUtilities.GetServiceOrCreateInstance(this, type);

        object? IServiceProvider.GetService(Type serviceType) =>
            serviceType == typeof(PerformContext)
                ? _context
                : _scope.ServiceProvider.GetService(serviceType);

        public override void DisposeScope()
        {
            // Dispose in reverse push order so LogContext unwinds cleanly.
            for (var i = _logContextProperties.Count - 1; i >= 0; i--)
            {
                _logContextProperties[i].Dispose();
            }

            _scope.Dispose();
        }
    }
}
