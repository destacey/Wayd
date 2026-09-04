using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Records the file fields an action declares, for <see cref="FormFileDocumentProcessor"/> to apply
/// once the multipart schema exists.
/// </summary>
/// <remarks>
/// This stage exists only because of what each stage can see. An operation processor is given the
/// action's <see cref="MethodInfo"/> — the sole remaining record of how many files it takes and which
/// are optional, since the generator merges every <see cref="IFormFile"/> into one flattened schema
/// and loses the distinction. A document processor can see the schema but not the method. Neither
/// alone is enough, so this one observes and the other one writes.
/// </remarks>
public sealed class FormFileOperationProcessor : IOperationProcessor
{
    /// <summary>One action's file fields, in declaration order.</summary>
    /// <param name="Name">The multipart field name, matching what model binding expects.</param>
    /// <param name="IsRequired">Whether the action requires the file.</param>
    public sealed record FileField(string Name, bool IsRequired);

    /// <summary>
    /// Keyed on the operation instance, which is the only thing both stages hold. Entries are removed
    /// as they are consumed, so a generator run cannot leave state behind for the next one — the
    /// document is regenerated on every Debug build and stale entries would outlive the operations
    /// they describe.
    /// </summary>
    private static readonly ConcurrentDictionary<OpenApiOperation, List<FileField>> _fileFields = new();

    public bool Process(OperationProcessorContext context)
    {
        if (context.MethodInfo is null)
        {
            return true;
        }

        var nullabilityContext = new NullabilityInfoContext();

        var fileFields = context.MethodInfo
            .GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile))
            .Select(p => new FileField(
                p.Name ?? "file",
                // A file is optional when the action says so by declaring it nullable, which is how
                // the strategic-initiative import marks its KPI file. Nothing in the generated
                // document records that, so a client built without it would demand a file that is
                // optional or accept the absence of one that is not.
                !p.HasDefaultValue && nullabilityContext.Create(p).WriteState != NullabilityState.Nullable))
            .ToList();

        if (fileFields.Count > 0)
        {
            _fileFields[context.OperationDescription.Operation] = fileFields;
        }

        return true;
    }

    /// <summary>
    /// Hands over the fields recorded for an operation, removing them so nothing is carried into a
    /// later run.
    /// </summary>
    internal static List<FileField> TakeFileFields(OpenApiOperation operation) =>
        _fileFields.TryRemove(operation, out var fields) ? fields : [];
}
