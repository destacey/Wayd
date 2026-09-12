using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Tests.Sut.Events;

/// <summary>
/// Every concrete domain event, and the reflection the convention tests share to inspect them.
/// </summary>
internal static partial class DomainEventCatalog
{
    public static readonly Type[] EventTypes = typeof(DomainEvent).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(DomainEvent)) && !t.IsAbstract && !t.IsGenericTypeDefinition)
        .ToArray();

    public static TheoryData<string> EventTypeNames() => new(EventTypes.Select(t => t.FullName!));

    public static Type ByName(string fullName) => EventTypes.Single(t => t.FullName == fullName);

    public static int Generation(Type type)
    {
        var match = GenerationSuffix().Match(type.Name);
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }

    /// <summary>The type name without its generation suffix: <c>ProjectRolesChangedEventV2</c> → <c>ProjectRolesChangedEvent</c>.</summary>
    public static string Stem(Type type) => GenerationSuffix().Replace(type.Name, string.Empty);

    /// <summary>
    /// The constructor System.Text.Json deserializes through: the one marked <c>[JsonConstructor]</c>, else a
    /// public parameterless one, else the only public one. <c>null</c> when none qualifies, which the
    /// serializer reports as an error at runtime.
    /// </summary>
    public static ConstructorInfo? DeserializationConstructor(Type type)
    {
        var annotated = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(c => c.GetCustomAttribute<JsonConstructorAttribute>() is not null)
            .ToArray();
        if (annotated.Length > 0)
            return annotated.Length == 1 ? annotated[0] : null;

        var publicCtors = type.GetConstructors();
        return publicCtors.FirstOrDefault(c => c.GetParameters().Length == 0)
            ?? (publicCtors.Length == 1 ? publicCtors[0] : null);
    }

    /// <summary>
    /// Builds an instance only to read values its constructor sets, such as the version it passes to
    /// <see cref="DomainEvent"/>; the argument values are irrelevant, they only have to get past the
    /// constructor body.
    /// </summary>
    public static DomainEvent Construct(Type type)
    {
        var ctor = type.GetConstructors()
            .OrderByDescending(c => c.GetCustomAttribute<JsonConstructorAttribute>() is not null)
            .ThenByDescending(c => c.GetParameters().Length)
            .First();

        var args = ctor.GetParameters().Select(p => Placeholder(p.ParameterType)).ToArray();

        return (DomainEvent)ctor.Invoke(args);
    }

    private static object? Placeholder(Type type)
    {
        if (type == typeof(EventActor)) return EventActor.System;
        if (type == typeof(string)) return string.Empty;
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) return Activator.CreateInstance(type);
        if (type.IsValueType) return Nullable.GetUnderlyingType(type) is null ? Activator.CreateInstance(type) : null;
        return null;
    }

    [GeneratedRegex(@"V(\d+)$")]
    private static partial Regex GenerationSuffix();
}
