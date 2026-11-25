using CsvHelper;
using Wayd.Common.Application.Exceptions;

namespace Wayd.Web.Api.Services;

/// <summary>
/// Reads an uploaded import file as its rows. A row type must be a <c>[CsvRecord]</c> partial type: CsvHelper's
/// source generator writes its mapper, and a type without one fails every file with a 500.
/// </summary>
public class CsvService : ICsvService
{
    /// <summary>
    /// The file format every import page documents, held strict where CsvHelper would otherwise guess: the
    /// separator is a comma rather than detected, broken quoting or a short row is refused rather than
    /// repaired, and a boolean is <c>true</c> or <c>false</c> rather than also <c>yes</c>, <c>y</c>, <c>t</c>
    /// or <c>1</c>. A damaged file is a 400 instead of rows that read differently from what was written.
    /// Headers still match ignoring case and surrounding whitespace, which CsvHelper offers no way to turn off.
    /// </summary>
    private static readonly CsvReaderOptions Options = CsvReaderOptions.Default with
    {
        Separator = ',',
        MalformedData = CsvMalformedDataHandling.Throw,
        MissingField = CsvMissingFieldHandling.Throw,
        TrueValues = ["true"],
        FalseValues = ["false"],
    };

    /// <summary>
    /// Reads every row of <paramref name="file"/>, and disposes it.
    /// </summary>
    /// <exception cref="CsvFileException">The file lacks a column, or a row cannot be read as <typeparamref name="T"/>.</exception>
    public IEnumerable<T> ReadCsv<T>(Stream file)
    {
        // Materialized inside the using scope: GetRecords reads lazily, so returning it would keep the
        // caller's request stream open until enumeration finished, and its exceptions would escape the catch.
        using var csv = CsvReader.Create(file, Options, leaveOpen: false);

        try
        {
            return [.. csv.GetRecords<T>()];
        }
        // CsvHelper has no common base: a missing header column is a KeyNotFoundException, a bad cell or broken
        // quoting a CsvFormatException, and the rest come straight from the parsers. InvalidOperationException
        // is left alone, since it means T has no generated mapper, which is a bug rather than a bad file.
        catch (Exception ex) when (ex is CsvFormatException or KeyNotFoundException or FormatException or IndexOutOfRangeException)
        {
            throw new CsvFileException(ex.Message, ex);
        }
    }
}
