namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Marks an action as the submission endpoint for an import definition, so the OpenAPI document can tell
/// a client which import it submits and what each of its files must contain.
/// </summary>
/// <remarks>
/// The rows each file carries are declared on the file parameter with <see cref="CsvRowsAttribute"/>.
/// <see cref="CsvImportOperationProcessor"/> publishes both as the operation's <c>x-wayd-import</c>
/// extension.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CsvImportAttribute(string importKey) : Attribute
{
    /// <summary>The key of the import definition the endpoint submits to.</summary>
    public string ImportKey { get; } = importKey;
}
