using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Names the document's component schemas, which the TypeScript client names its interfaces after.
/// </summary>
/// <remarks>
/// A generic type is named for its type arguments:
/// <c>NavigationDto&lt;Guid, int&gt;</c> is <c>NavigationDtoOfGuidAndInteger</c> and
/// <c>PagedResponse&lt;ActivityLogDto&gt;</c> is <c>PagedResponseOfActivityLogDto</c>. Types
/// <see cref="ScalarSchemaTransformer"/> describes as strings are never components.
/// </remarks>
public static class SchemaReferenceIds
{
    /// <summary>The name of a primitive type argument: its JSON Schema type, not its CLR name.</summary>
    private static readonly Dictionary<Type, string> _primitiveNames = new()
    {
        [typeof(int)] = "Integer",
        [typeof(long)] = "Long",
        [typeof(string)] = "String",
        [typeof(bool)] = "Boolean",
        [typeof(double)] = "Double",
        [typeof(decimal)] = "Decimal",
        [typeof(Guid)] = "Guid",
    };

    /// <summary>
    /// The component name for a type, or null to describe it inline wherever it is used. Plugged into
    /// <see cref="OpenApiOptions.CreateSchemaReferenceId"/>.
    /// </summary>
    public static string? Create(JsonTypeInfo typeInfo)
    {
        if (ScalarSchemaTransformer.Describes(typeInfo.Type))
            return null;

        // A nullable enum is the enum's own component, made nullable where it is used.
        var id = OpenApiOptions.CreateDefaultSchemaReferenceId(typeInfo);
        return id is not null && typeInfo.Type.IsGenericType && Nullable.GetUnderlyingType(typeInfo.Type) is null ? For(typeInfo.Type) : id;
    }

    /// <summary>The component name for a type the document describes as an object or an enum.</summary>
    public static string For(Type type)
    {
        if (_primitiveNames.TryGetValue(type, out var primitive))
            return primitive;

        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}Of{string.Join("And", type.GetGenericArguments().Select(For))}";
    }
}
