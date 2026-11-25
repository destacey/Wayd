using System.Text;
using CsvHelper;
using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The date format every generated file is written in.
/// </summary>
/// <remarks>
/// CsvHelper writes every date and time in ISO 8601's round-trip format, which for a <see cref="DateTime"/>
/// carries a time of day and for a <see cref="DateTimeOffset"/> fractional seconds. A column says otherwise
/// with <c>[CsvFormat]</c>, so these pin that the formats the import docs state are what reaches the file.
/// </remarks>
public partial class CsvFileTests
{
    [CsvRecord]
    internal sealed partial class DatedRow
    {
        public DateOnly Required { get; init; }
        public DateOnly? Optional { get; init; }

        [CsvFormat(CsvFormats.Date)]
        public DateTime? Day { get; init; }

        [CsvFormat(CsvFormats.Instant)]
        public DateTimeOffset? Instant { get; init; }
    }

    [Fact]
    public void ToBytes_WritesDatesAndInstantsInTheDocumentedFormats()
    {
        // Arrange
        var rows = new[]
        {
            new DatedRow
            {
                Required = new DateOnly(2026, 1, 5),
                Optional = new DateOnly(2026, 11, 30),
                Day = new DateTime(2026, 2, 7),
                Instant = new DateTimeOffset(2026, 2, 7, 14, 3, 0, TimeSpan.Zero),
            },
        };

        // Act
        var csv = Encoding.UTF8.GetString(CsvFile.ToBytes(rows));

        // Assert
        csv.Should().Contain("2026-01-05,2026-11-30,2026-02-07,2026-02-07T14:03:00+00:00");
    }

    [Fact]
    public void ToBytes_LeavesAnEmptyOptionalDateBlank()
    {
        // Arrange
        var rows = new[] { new DatedRow { Required = new DateOnly(2026, 1, 5) } };

        // Act
        var csv = Encoding.UTF8.GetString(CsvFile.ToBytes(rows));

        // Assert
        csv.Should().Contain("2026-01-05,,,");
    }

    [CsvRecord]
    internal sealed partial class TextRow
    {
        public string? Text { get; init; }
    }

    [Fact]
    public void ToBytes_QuotesTextWithLeadingOrTrailingWhitespace()
    {
        // Arrange
        var rows = new[] { new TextRow { Text = " padded " } };

        // Act
        var csv = Encoding.UTF8.GetString(CsvFile.ToBytes(rows));

        // Assert
        csv.Should().Contain("\" padded \"");
    }

    public static TheoryData<Type> UploadRowTypes() =>
        [.. typeof(CsvFile).Assembly.GetTypes().Where(t => t.Namespace == typeof(CsvFile).Namespace && t.Name.EndsWith("CsvRow"))];

    [Theory]
    [MemberData(nameof(UploadRowTypes))]
    public void ToBytes_WritesEveryUploadRowType(Type rowType)
    {
        // Arrange — a row type without [CsvRecord] has no generated writer, and fails only when a seed posts it
        var rows = Array.CreateInstance(rowType, 0);
        var toBytes = typeof(CsvFile).GetMethod(nameof(CsvFile.ToBytes))!.MakeGenericMethod(rowType);

        // Act
        var csv = Encoding.UTF8.GetString((byte[])toBytes.Invoke(null, [rows])!);

        // Assert — the header alone, naming the row's columns
        csv.Should().NotBeEmpty();
    }
}
