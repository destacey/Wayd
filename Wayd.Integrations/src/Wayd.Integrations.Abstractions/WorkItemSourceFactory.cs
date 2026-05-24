using Microsoft.Extensions.DependencyInjection;

namespace Wayd.Integrations.Abstractions;

internal sealed class WorkItemSourceFactory(IServiceProvider serviceProvider) : IWorkItemSourceFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Result<IWorkItemSource> Create(SyncableConnectionDescriptor descriptor)
    {
        var source = _serviceProvider.GetKeyedService<IWorkItemSource>(descriptor.Connector);
        if (source is null)
            return Result.Failure<IWorkItemSource>($"No IWorkItemSource is registered for connector '{descriptor.Connector}'.");

        var bind = source.Bind(descriptor);
        return bind.IsFailure
            ? Result.Failure<IWorkItemSource>(bind.Error)
            : Result.Success(source);
    }
}
