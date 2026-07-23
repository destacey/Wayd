using System.Globalization;
using CsvHelper;

namespace Wayd.Web.Api.Services;

public class CsvService : ICsvService
{
    public IEnumerable<T> ReadCsv<T>(Stream file)
    {
        // Own and dispose the reader/CsvReader here, and materialize the records before returning. GetRecords
        // reads lazily, so returning it directly would leave the StreamReader (and the caller's request
        // stream) open until enumeration completed — and callers cannot dispose a reader they never see.
        // Reading eagerly inside the using scope closes everything as soon as parsing finishes.
        using var reader = new StreamReader(file);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        return [.. csv.GetRecords<T>()];
    }
}
