using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Common.Domain.Following;

/// <summary>
/// The kinds of record the running application lets people follow, contributed by whichever modules
/// offer following.
/// </summary>
/// <remarks>
/// A registry rather than an enum, the convention <c>WorkflowOwners</c> set: following holds strings it
/// never interprets, and a module joining it adds a file to its own project.
/// <para>
/// Register once at startup. Idempotent for the same descriptor instance; conflicting registrations for
/// one key throw rather than taking the last writer.
/// </para>
/// </remarks>
public static class FollowOwners
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, FollowOwnerDescriptor> Descriptors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a module's followable owner types. Safe to call more than once with the same descriptors.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A different descriptor is already registered under one of the keys.
    /// </exception>
    public static void Register(params FollowOwnerDescriptor[] descriptors)
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
                        $"A different followable owner type is already registered as '{descriptor.Key}'. Keys are persisted on follow rows and must be unique across modules.");
                }

                Descriptors[descriptor.Key] = descriptor;
            }
        }
    }

    /// <summary>
    /// Resolves a registered owner type, or fails when the key is unknown.
    /// </summary>
    /// <remarks>
    /// A <see cref="Result{T}"/> rather than a throw: the key arrives from a caller or the database, so an
    /// unknown one is a bad request or a removed module, not a crash.
    /// </remarks>
    public static Result<FollowOwnerDescriptor> Resolve(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<FollowOwnerDescriptor>("A followable owner type is required.");
        }

        lock (Gate)
        {
            return Descriptors.TryGetValue(key.Trim(), out var descriptor)
                ? Result.Success(descriptor)
                : Result.Failure<FollowOwnerDescriptor>($"'{key.Trim()}' is not a kind of record that can be followed.");
        }
    }

    /// <summary>Every registered owner type, ordered by display name.</summary>
    public static IReadOnlyCollection<FollowOwnerDescriptor> All
    {
        get
        {
            lock (Gate)
            {
                return Descriptors.Values.OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
            }
        }
    }
}
