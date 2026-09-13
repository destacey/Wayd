using System.Text;
using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The date format every generated file is written in.
/// </summary>
/// <remarks>
/// CsvHelper keys its format options by type, so a <see cref="DateOnly"/> column does not pick up an entry
/// made for <see cref="DateTime"/>. Left unconfigured it writes <c>01/05/2026</c>, which still parses under
/// the invariant culture but contradicts the <c>yyyy-MM-dd</c> the import docs state.
/// </remarks>
public class CsvFileTests
{
    private sealed class DatedRow
    {
        public DateOnly Required { get; init; }
        public DateOnly? Optional { get; init; }
        public DateTime? Instant { get; init; }
    }

    [Fact]
    public void ToBytes_WritesDateOnlyAndDateTimeColumnsAsIso()
    {
        // Arrange
        var rows = new[]
        {
            new DatedRow
            {
                Required = new DateOnly(2026, 1, 5),
                Optional = new DateOnly(2026, 11, 30),
                Instant = new DateTime(2026, 2, 7),
            },
        };

        // Act
        var csv = Encoding.UTF8.GetString(CsvFile.ToBytes(rows));

        // Assert
        csv.Should().Contain("2026-01-05,2026-11-30,2026-02-07");
    }

    [Fact]
    public void ToBytes_LeavesAnEmptyOptionalDateBlank()
    {
        // Arrange
        var rows = new[] { new DatedRow { Required = new DateOnly(2026, 1, 5) } };

        // Act
        var csv = Encoding.UTF8.GetString(CsvFile.ToBytes(rows));

        // Assert
        csv.Should().Contain("2026-01-05,,");
    }
}
