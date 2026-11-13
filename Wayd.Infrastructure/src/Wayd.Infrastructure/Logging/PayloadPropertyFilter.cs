using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Serilog.Exceptions.Filters;
using Wolverine;

namespace Wayd.Infrastructure.Logging;

/// <summary>
/// Leaves out of an exception's logged details any property declared as a message envelope, a
/// change-tracker entry or a database context, or a collection of them.
/// </summary>
/// <remarks>
/// An exception with no destructurer of its own is walked property by property, and these three lead
/// straight to data: an <see cref="Envelope"/> carries the whole message, an <see cref="EntityEntry"/> the
/// tracked entity and its graph, a <see cref="DbContext"/> everything it tracks. Wolverine's inbox and
/// dead-letter exceptions hold envelopes. The check is on the declared property type because the walk
/// hands the filter a value it has already turned into lists and dictionaries.
/// </remarks>
public sealed class PayloadPropertyFilter : IExceptionPropertyFilter
{
    private static readonly Type[] _payloadTypes = [typeof(Envelope), typeof(EntityEntry), typeof(DbContext)];

    /// <inheritdoc />
    public bool ShouldPropertyBeFiltered(Exception exception, string propertyName, object? value)
    {
        // DbUpdateExceptionDestructurer has already replaced Entries with each entry's type and state.
        if (exception is DbUpdateException)
            return false;

        var property = exception.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        return property is not null && CarriesPayload(property.PropertyType);
    }

    private static bool CarriesPayload(Type type) =>
        IsPayloadType(type)
        || (type.IsArray && IsPayloadType(type.GetElementType()!))
        || EnumeratedTypes(type).Any(IsPayloadType);

    private static IEnumerable<Type> EnumeratedTypes(Type type) =>
        (type.IsInterface ? type.GetInterfaces().Append(type) : type.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0]);

    private static bool IsPayloadType(Type type) => _payloadTypes.Any(t => t.IsAssignableFrom(type));
}
