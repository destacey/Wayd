namespace Wayd.Infrastructure.Tests.Infrastructure;

/// <summary>
/// Serializes every test class that installs a protector into <c>SecretProtectorAccessor</c>.
/// <para>
/// The accessor is process-wide and documented as "set once during app startup", so nothing in
/// production ever races on it. Tests do: each class sets its own protector with a freshly generated
/// key in its constructor, and xUnit runs different classes in parallel. A class that protects a value
/// under its key and then reads it back after another class has swapped the accessor decrypts with the
/// wrong key and fails with <c>AuthenticationTagMismatchException</c> — intermittently, and far more
/// often on CI than on a developer machine, because the interleaving depends on core count and timing.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SecretProtectorCollection
{
    public const string Name = "SecretProtector";
}
