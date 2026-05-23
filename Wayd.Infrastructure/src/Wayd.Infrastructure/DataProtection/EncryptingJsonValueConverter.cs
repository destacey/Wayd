using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wayd.Common.Domain.DataProtection;

namespace Wayd.Infrastructure.DataProtection;

/// <summary>
/// EF Core value converter that serializes <typeparamref name="T"/> to JSON for
/// storage, encrypting any string properties marked <c>[Encrypted]</c> (including
/// those on nested objects and inside collections) before the JSON column is
/// written, and decrypting them on read.
///
/// Use via <c>HasEncryptedJsonConversion()</c> on a property builder.
///
/// Plaintext detected on read (legacy rows from before encryption rolled out)
/// is returned as-is — the startup backfill is responsible for upgrading those.
/// </summary>
public sealed class EncryptingJsonValueConverter<T> : ValueConverter<T, string>
{
    public EncryptingJsonValueConverter(JsonSerializerOptions? options = null)
        : base(
            v => SerializeAndEncrypt(v, options),
            v => DeserializeAndDecrypt(v, options)!)
    {
    }

    private static string SerializeAndEncrypt(T value, JsonSerializerOptions? options)
    {
        WalkEncryptedStrings(value, plaintext => SecretProtectorAccessor.Current.Protect(plaintext));
        return JsonSerializer.Serialize(value, options);
    }

    private static T? DeserializeAndDecrypt(string json, JsonSerializerOptions? options)
    {
        var value = JsonSerializer.Deserialize<T>(json, options);
        WalkEncryptedStrings(value, ciphertext =>
            SecretProtectorAccessor.Current.IsProtected(ciphertext)
                ? SecretProtectorAccessor.Current.Unprotect(ciphertext)
                : ciphertext);
        return value;
    }

    private static void WalkEncryptedStrings(object? root, Func<string, string> transform)
    {
        if (root is null) return;
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        WalkObject(root, transform, visited);
    }

    private static void WalkObject(object obj, Func<string, string> transform, HashSet<object> visited)
    {
        if (obj is null || !visited.Add(obj)) return;

        var type = obj.GetType();
        if (IsLeafType(type)) return;

        // Collections — walk items
        if (obj is IEnumerable enumerable && obj is not string)
        {
            foreach (var item in enumerable)
            {
                if (item is null) continue;
                WalkObject(item, transform, visited);
            }
            return;
        }

        foreach (var prop in GetWalkableProperties(type))
        {
            var value = prop.Getter(obj);
            if (value is null) continue;

            if (prop.IsEncryptedString)
            {
                var s = (string)value;
                if (string.IsNullOrEmpty(s)) continue;
                var transformed = transform(s);
                if (!ReferenceEquals(transformed, s))
                {
                    prop.Setter?.Invoke(obj, transformed);
                }
                continue;
            }

            if (!IsLeafType(prop.PropertyType))
            {
                WalkObject(value, transform, visited);
            }
        }
    }

    private static bool IsLeafType(Type type)
        => type.IsPrimitive
           || type.IsEnum
           || type == typeof(string)
           || type == typeof(decimal)
           || type == typeof(DateTime)
           || type == typeof(DateTimeOffset)
           || type == typeof(Guid)
           || type == typeof(TimeSpan);

    private sealed record WalkProp(
        Type PropertyType,
        bool IsEncryptedString,
        Func<object, object?> Getter,
        Action<object, object?>? Setter);

    private static readonly ConcurrentDictionary<Type, WalkProp[]> _cache = new();

    private static WalkProp[] GetWalkableProperties(Type type)
        => _cache.GetOrAdd(type, t =>
        {
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var result = new List<WalkProp>(props.Length);
            foreach (var p in props)
            {
                if (p.GetIndexParameters().Length > 0) continue;
                var getter = p.GetGetMethod(nonPublic: true);
                if (getter is null) continue;
                var setter = p.GetSetMethod(nonPublic: true);

                var isEncrypted = p.PropertyType == typeof(string)
                    && p.IsDefined(typeof(EncryptedAttribute), inherit: true);

                // Only include props worth walking: encrypted strings, or complex types
                // (anything that isn't a leaf). Skip plain leaf properties entirely.
                if (!isEncrypted && IsLeafType(p.PropertyType)) continue;

                result.Add(new WalkProp(
                    p.PropertyType,
                    isEncrypted,
                    instance => getter.Invoke(instance, null),
                    setter is null ? null : (instance, value) => setter.Invoke(instance, new[] { value })));
            }
            return result.ToArray();
        });
}
