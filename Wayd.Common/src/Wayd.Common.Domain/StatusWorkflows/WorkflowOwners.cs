using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// The owner types the running application knows about, contributed by whichever modules use the
/// workflow engine.
/// </summary>
/// <remarks>
/// A registry rather than an enum so the engine holds strings it never interprets and a module joining
/// it adds a file to its own project.
/// <para>
/// Register once at startup, before anything resolves a workflow. Idempotent for the same descriptor
/// instance; conflicting registrations for one key throw rather than taking the last writer.
/// </para>
/// </remarks>
public static class WorkflowOwners
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, WorkflowOwnerDescriptor> Descriptors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a module's owner types. Safe to call more than once with the same descriptors.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A different descriptor is already registered under one of the keys.
    /// </exception>
    public static void Register(params WorkflowOwnerDescriptor[] descriptors)
    {
        Guard.Against.Null(descriptors, nameof(descriptors));

        lock (Gate)
        {
            foreach (var descriptor in descriptors)
            {
                Guard.Against.Null(descriptor, nameof(descriptors));

                if (Descriptors.TryGetValue(descriptor.Key, out var existing))
                {
                    if (ReferenceEquals(existing, descriptor))
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"A different workflow owner type is already registered as '{descriptor.Key}'. Keys are persisted on workflow rows and must be unique across modules.");
                }

                Descriptors[descriptor.Key] = descriptor;
            }
        }
    }

    /// <summary>
    /// Resolves a registered owner type, or fails when the key is unknown.
    /// </summary>
    /// <remarks>
    /// A <see cref="Result{T}"/> rather than a throw: the key usually comes from the database, so a
    /// workflow whose module has been removed must be diagnosable rather than an unhandled exception.
    /// </remarks>
    public static Result<WorkflowOwnerDescriptor> Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<WorkflowOwnerDescriptor>("A workflow owner type is required.");
        }

        lock (Gate)
        {
            return Descriptors.TryGetValue(key.Trim(), out var descriptor)
                ? Result.Success(descriptor)
                : Result.Failure<WorkflowOwnerDescriptor>(
                    $"'{key.Trim()}' is not a registered workflow owner type. Its module may not be registered.");
        }
    }

    /// <summary>
    /// Whether a key resolves to a registered owner type.
    /// </summary>
    public static bool IsRegistered(string key) => Resolve(key).IsSuccess;

    /// <summary>Every registered owner type, for an admin screen.</summary>
    public static IReadOnlyCollection<WorkflowOwnerDescriptor> All
    {
        get
        {
            lock (Gate)
            {
                return Descriptors.Values.OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
            }
        }
    }

    /// <summary>Clears the registry. Test seam only.</summary>
    internal static void Reset()
    {
        lock (Gate)
        {
            Descriptors.Clear();
        }
    }
}
