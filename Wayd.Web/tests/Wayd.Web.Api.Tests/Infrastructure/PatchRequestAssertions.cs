using System.Collections;
using System.Reflection;

namespace Wayd.Web.Api.Tests.Infrastructure;

/// <summary>
/// Guards the conversion a JSON Patch endpoint applies its patch to: the current record turned into its update
/// request. A request property the conversion leaves unset is cleared by every patch, so converting a fully
/// populated record must set every property.
/// </summary>
public static class PatchRequestAssertions
{
    /// <summary>
    /// Fails, naming each one, when a public settable property of <paramref name="request"/> holds its type's
    /// default — <see langword="null"/>, zero, an empty string or an empty collection.
    /// </summary>
    /// <param name="request">The update request converted from a record whose every property is non-default.</param>
    /// <param name="exclusions">
    /// Properties that legitimately have no source on the record. Each needs a comment at the call site saying why.
    /// </param>
    public static void ShouldSetEveryProperty(object request, params string[] exclusions)
    {
        var unknownExclusions = exclusions
            .Where(name => request.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance) is null)
            .ToList();
        if (unknownExclusions.Count > 0)
            throw new InvalidOperationException(
                $"{request.GetType().Name} has no property named {string.Join(", ", unknownExclusions)} to exclude.");

        var unset = request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod!.IsPublic && p.GetIndexParameters().Length == 0)
            .Where(p => !exclusions.Contains(p.Name))
            .Where(p => IsDefault(p.GetValue(request), p.PropertyType))
            .Select(p => p.Name)
            .ToList();

        if (unset.Count > 0)
            throw new Xunit.Sdk.XunitException(
                $"Converting a fully populated record to {request.GetType().Name} left {string.Join(", ", unset)} unset, " +
                "so every patch would clear it. Copy it in the conversion, or exclude it with a reason if the record has no source for it.");
    }

    private static bool IsDefault(object? value, Type type) => value switch
    {
        null => true,
        string text => text.Length == 0,
        IEnumerable items => !items.GetEnumerator().MoveNext(),
        _ => type.IsValueType && value.Equals(Activator.CreateInstance(Nullable.GetUnderlyingType(type) ?? type)),
    };
}
