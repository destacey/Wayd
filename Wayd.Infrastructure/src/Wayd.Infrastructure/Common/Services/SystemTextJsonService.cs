using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using OneOf.Serialization.SystemTextJson;

namespace Wayd.Infrastructure.Common.Services;

public sealed class SystemTextJsonService : ISerializerService
{
    // One instance, built once. System.Text.Json caches its type metadata per options object, so
    // constructing fresh options per call — as this used to — threw that cache away on a path that runs
    // for every slow request.
    private static readonly JsonSerializerOptions _options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.Preserve,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new TypeConverter(),
                new OneOfJsonConverter(),
                new OneOfBaseJsonConverter(),
            }
        };

        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

        return options;
    }

    public string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, _options);
}

public class TypeConverter : JsonConverter<Type>
{
    public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var typeName = reader.GetString();
        return Type.GetType(typeName!)!;
    }

    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.AssemblyQualifiedName);
    }
}
