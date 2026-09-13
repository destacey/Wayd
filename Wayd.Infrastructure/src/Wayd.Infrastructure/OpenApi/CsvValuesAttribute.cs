namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Declares the values a text column accepts: the names of the enum the row parses the cell into.
/// </summary>
/// <remarks>
/// The column stays text in the document — its validator parses it case-insensitively, and some columns
/// also accept a blank — so the names are published as <c>x-csv-values</c> rather than as a schema enum,
/// which would retype the property in every generated client.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class CsvValuesAttribute(Type enumType) : Attribute
{
    public Type EnumType { get; } = enumType;

    public IReadOnlyList<string> Values => Enum.GetNames(EnumType);
}
