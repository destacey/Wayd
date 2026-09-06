using System.Text.Json.Serialization;
using Wayd.Common.Serialization;

namespace Wayd.Common.Models;

[JsonConverter(typeof(ScalarValueObjectJsonConverterFactory))]
public class Progress : ScalarValueObject<decimal>
{
    public Progress(decimal value) : base(Validate(value))
    {
    }

    private static decimal Validate(decimal value)
    {
        if (value < 0.0m || value > 100.0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Progress must be between 0.0 and 100.0");
        }

        return value;
    }

    public static Progress NotStarted() => new(0.0m);
    public static Progress Completed() => new(100.0m);

    public override string ToString() => $"{Value}%";

    public static explicit operator Progress(decimal value) => new(value);
}
