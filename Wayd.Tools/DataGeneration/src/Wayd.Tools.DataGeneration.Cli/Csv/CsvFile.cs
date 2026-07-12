using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Wayd.Tools.DataGeneration.Cli.Csv;

/// <summary>Writes strongly-typed rows to CSV, either to a file or into an in-memory stream for upload.</summary>
public static class CsvFile
{
    private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture);

    public static void Write<T>(string path, IEnumerable<T> rows)
    {
        using var writer = new StreamWriter(path);
        WriteTo(writer, rows);
    }

    /// <summary>Serializes the rows to a UTF-8 CSV byte array (for multipart upload).</summary>
    public static byte[] ToBytes<T>(IEnumerable<T> rows)
    {
        using var memory = new MemoryStream();
        using (var writer = new StreamWriter(memory, leaveOpen: true))
        {
            WriteTo(writer, rows);
        }
        return memory.ToArray();
    }

    private static void WriteTo<T>(TextWriter writer, IEnumerable<T> rows)
    {
        using var csv = new CsvWriter(writer, Configuration);

        // Write dates in unambiguous ISO 8601 (yyyy-MM-dd). The API parses these with InvariantCulture and
        // only needs the date component (team ActiveDate/InactiveDate are LocalDate; employee HireDate
        // becomes an Instant at UTC midnight).
        var isoFormats = new[] { "yyyy-MM-dd" };
        csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = isoFormats;
        csv.Context.TypeConverterOptionsCache.GetOptions<DateTime?>().Formats = isoFormats;

        csv.WriteRecords(rows);
    }
}
