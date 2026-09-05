namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Serializes an object for logging.
/// </summary>
/// <remarks>
/// Write-only on purpose. It previously also exposed a <c>Deserialize</c> and a second <c>Serialize</c>
/// overload, both uncalled, and both configured differently from the one that was used — so a round trip
/// through it silently returned an object with every property unset. Anything that needs to read a payload
/// back should own a serializer whose read and write options are the same instance; see
/// <c>ImportPayloadSerializer</c>.
/// </remarks>
public interface ISerializerService : ITransientService
{
    string Serialize<T>(T obj);
}
