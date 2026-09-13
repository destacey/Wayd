namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Names the class a CSV file parameter is read into. Every settable public property is a column the
/// file must carry, under the property's own name.
/// </summary>
/// <remarks>
/// Must name the same type the action passes to <c>ReadCsv</c>. The file's shape is otherwise known only
/// inside the method body, where neither the document generator nor a test can see it.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class CsvRowsAttribute(Type rowType) : Attribute
{
    public Type RowType { get; } = rowType;

    /// <summary>
    /// What the file is, where the action takes more than one — "Manifest". The main file needs none: it
    /// is the import itself.
    /// </summary>
    public string? Label { get; init; }
}
