using Microsoft.Extensions.DependencyInjection;

namespace Wayd.Integrations.Abstractions;

internal sealed class EmployeeSourceFactory(IServiceProvider serviceProvider) : IEmployeeSourceFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Result<IEmployeeSource> Create(SyncableConnectionDescriptor descriptor)
    {
        var source = _serviceProvider.GetKeyedService<IEmployeeSource>(descriptor.Connector);
        if (source is null)
            return Result.Failure<IEmployeeSource>($"No IEmployeeSource is registered for connector '{descriptor.Connector}'.");

        var bind = source.Bind(descriptor);
        return bind.IsFailure
            ? Result.Failure<IEmployeeSource>(bind.Error)
            : Result.Success(source);
    }
}
