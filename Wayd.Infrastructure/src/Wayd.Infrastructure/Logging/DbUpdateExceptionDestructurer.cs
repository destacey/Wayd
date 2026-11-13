using Microsoft.EntityFrameworkCore;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.Destructurers;

namespace Wayd.Infrastructure.Logging;

/// <summary>
/// Destructures a <see cref="DbUpdateException"/> for <c>Serilog.Exceptions</c> as the entity type and
/// state of each failed entry, and nothing about the values it held.
/// </summary>
/// <remarks>
/// Without a destructurer for this type, the reflection-based fallback walks <see cref="DbUpdateException.Entries"/>
/// into each tracked entity, its navigations and the <see cref="DbContext"/> itself. Do not replace this
/// with the destructurer <c>Serilog.Exceptions.EntityFrameworkCore</c> ships: it logs every property's
/// original and current value, stored secrets and personal data included.
/// </remarks>
public sealed class DbUpdateExceptionDestructurer : ExceptionDestructurer
{
    /// <inheritdoc />
    public override Type[] TargetTypes => [typeof(DbUpdateException), typeof(DbUpdateConcurrencyException)];

    /// <inheritdoc />
    public override void Destructure(
        Exception exception,
        IExceptionPropertiesBag propertiesBag,
        Func<Exception, IReadOnlyDictionary<string, object?>?> destructureException)
    {
        base.Destructure(exception, propertiesBag, destructureException);

        var entries = ((DbUpdateException)exception).Entries
            .Select(e => new Dictionary<string, string>
            {
                ["EntityType"] = e.Metadata.DisplayName(),
                ["State"] = e.State.ToString(),
            })
            .ToList();

        propertiesBag.AddProperty(nameof(DbUpdateException.Entries), entries);
    }
}
