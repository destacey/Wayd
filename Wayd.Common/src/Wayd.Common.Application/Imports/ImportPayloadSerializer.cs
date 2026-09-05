using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Application.Interfaces;

namespace Wayd.Common.Application.Imports;

public interface IImportPayloadSerializer : IScopedService
{
    string Serialize<T>(T row);
    T Deserialize<T>(string payload);
}

/// <summary>
/// Serializes import row payloads.
/// </summary>
/// <remarks>
/// Deliberately its own serializer rather than the shared <see cref="ISerializerService"/>. Two reasons:
/// that one is asymmetric — it serializes with a camel-case policy and deserializes with none, which is
/// harmless for its only other caller (<c>PerformanceBehavior</c> serializes for logging and never reads
/// back) but silently returns an empty object here. And an import payload has a requirement a
/// general-purpose serializer does not: it is written at submission and read back by a worker that may run
/// days later, after a resume, so its format must not move because someone tuned the options for logging.
/// <para>
/// One options instance, used for both directions, with case-insensitive reads so a payload stays readable
/// even if the write policy is ever changed.
/// </para>
/// </remarks>
public sealed class ImportPayloadSerializer : IImportPayloadSerializer
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        // Row DTOs carry Instant, LocalDate and the like; without this they round-trip as empty objects.
        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

        return options;
    }

    public string Serialize<T>(T row) => JsonSerializer.Serialize(row, _options);

    public T Deserialize<T>(string payload) => JsonSerializer.Deserialize<T>(payload, _options)!;
}
