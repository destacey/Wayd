using System.Text;
using CsvHelper;
using FluentAssertions;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.Tests.Sut.Services;

/// <summary>
/// What a CSV cell becomes, for the column types the imports actually declare.
/// </summary>
/// <remarks>
/// The date cases are the point: every import's date-only column is a <see cref="DateOnly"/>, so the type
/// says a date is a date. This pins what that means at the boundary — a cell carrying a time is refused
/// rather than silently truncated to midnight, and the exception is a <see cref="CsvHelperException"/>,
/// which is what every import endpoint catches and answers 400 with.
/// </remarks>
public sealed class CsvServiceTests
{
    private sealed class Row
    {
        public string Name { get; set; } = default!;
        public DateOnly Start { get; set; }
        public DateOnly? End { get; set; }
    }

    private readonly CsvService _csvService = new();

    private IEnumerable<Row> Read(string csv) =>
        _csvService.ReadCsv<Row>(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

    [Fact]
    public void ReadCsv_ReadsADateOnlyColumn()
    {
        // Arrange & Act
        var rows = Read("Name,Start,End\nPI 2026.1,2026-01-05,2026-02-15\n").ToList();

        // Assert
        var row = rows.Single();
        row.Start.Should().Be(new DateOnly(2026, 1, 5));
        row.End.Should().Be(new DateOnly(2026, 2, 15));
    }

    [Fact]
    public void ReadCsv_ReadsABlankNullableDateAsNull()
    {
        // Arrange & Act — every column must be present, so an unused optional date is an empty cell
        var rows = Read("Name,Start,End\nPI 2026.1,2026-01-05,\n").ToList();

        // Assert
        rows.Single().End.Should().BeNull();
    }

    [Fact]
    public void ReadCsv_RefusesATimestampInADateColumn()
    {
        // Arrange & Act — the column means a date, so a time is a mistake in the file rather than
        // something to throw away
        var read = () => Read("Name,Start,End\nPI 2026.1,2026-01-05T00:00:00Z,\n").ToList();

        // Assert
        read.Should().Throw<CsvHelperException>();
    }
}
