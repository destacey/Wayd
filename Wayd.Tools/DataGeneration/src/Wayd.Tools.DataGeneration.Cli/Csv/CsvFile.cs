using CsvHelper;

namespace Wayd.Tools.DataGeneration.Cli.Csv;

/// <summary>Writes strongly-typed rows to CSV, either to a file or into an in-memory stream for upload.</summary>
/// <remarks>
/// A row type must be a <c>[CsvRecord]</c> partial type: CsvHelper's source generator writes its columns, and
/// a type without one throws when it is written. Dates are written in ISO 8601 (<c>yyyy-MM-dd</c>), which every
/// bulk-import page documents; a <see cref="DateTime"/> or <see cref="DateTimeOffset"/> column that should be
/// written differently says so with <c>[CsvFormat]</c>.
/// </remarks>
public static class CsvFile
{
    /// <summary>
    /// Quotes a cell that starts or ends with whitespace too, so the text survives a reader that trims
    /// unquoted cells.
    /// </summary>
    private static readonly CsvWriterOptions Options = CsvWriterOptions.Default with { Quoting = CsvQuoting.WhenNeededOrWhitespace };

    /// <summary>Writes the rows, with a header, to the file at <paramref name="path"/>, replacing it.</summary>
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
        using var csv = CsvWriter.Create(writer, Options, leaveOpen: true);
        csv.WriteRecords(rows);
    }
}

/// <summary>The formats that a generated row's <c>[CsvFormat]</c> columns are written in.</summary>
public static class CsvFormats
{
    /// <summary>
    /// An instant with its offset, to the second. CsvHelper's own ISO 8601 adds fractional seconds, which the
    /// files have no use for.
    /// </summary>
    public const string Instant = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>
    /// A date alone, for a <see cref="DateTime"/> column that holds one: CsvHelper writes a time of day otherwise.
    /// </summary>
    public const string Date = "yyyy-MM-dd";
}
