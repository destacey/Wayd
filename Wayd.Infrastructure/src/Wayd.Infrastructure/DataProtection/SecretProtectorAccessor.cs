namespace Wayd.Infrastructure.DataProtection;

/// <summary>
/// Process-wide accessor for the <see cref="ISecretProtector"/>.
///
/// EF Core's <c>IEntityTypeConfiguration</c> classes are instantiated by reflection
/// during <c>OnModelCreating</c> and have no access to the DI container. This static
/// accessor is set once during app startup (in <c>AddDataProtection</c>) before any
/// EF query runs.
/// </summary>
public static class SecretProtectorAccessor
{
    private static ISecretProtector? _instance;

    public static ISecretProtector Current
        => _instance ?? throw new InvalidOperationException(
            "ISecretProtector has not been initialized. Ensure AddDataProtection() runs at startup before any EF query.");

    internal static void Set(ISecretProtector protector)
    {
        _instance = protector ?? throw new ArgumentNullException(nameof(protector));
    }
}
