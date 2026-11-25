using System.Reflection;
using System.Text;
using CsvHelper;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Wayd.Common.Application.Exceptions;
using Wayd.Infrastructure.OpenApi;
using Wayd.Web.Api.Models.DemandManagement.IntakeRequests;
using Wayd.Web.Api.Models.ProductManagement.Versions;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.Tests.Sut.Services;

/// <summary>
/// What a CSV cell becomes, for the column types the imports actually declare.
/// </summary>
/// <remarks>
/// The date cases are the point: every import's date-only column is a <see cref="DateOnly"/>, so the type
/// says a date is a date. This pins what that means at the boundary — a cell carrying a time is refused
/// rather than silently truncated to midnight, and the exception is a <see cref="CsvFileException"/>,
/// which is what every import endpoint catches and answers 400 with.
/// </remarks>
public sealed partial class CsvServiceTests
{
    [CsvRecord]
    internal sealed partial class Row
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
    public void ReadCsv_RefusesAFileMissingAColumn()
    {
        // Arrange & Act — even a column whose every cell could be blank must have its header
        var read = () => Read("Name,Start\nPI 2026.1,2026-01-05\n").ToList();

        // Assert — the 400 names the column, so the person fixing the file knows what to add
        read.Should().Throw<CsvFileException>().WithMessage("*'End'*");
    }

    [Fact]
    public void ReadCsv_RefusesAnImportFileWithoutAnImportIdColumn()
    {
        // Arrange & Act — the import key falls back to the row's position only for a blank cell
        var read = () => ReadVersions("ProductId,Number,Name,TargetDate,CutAt,ReleasedAt,Sequence,Notes\n"
            + "17f79eeb-d355-4118-9cb0-166b213dd916,1.0.0,Initial release,,,,,\n");

        // Assert
        read.Should().Throw<CsvFileException>();
    }

    [Fact]
    public void ReadCsv_ReadsABlankImportIdAsEmpty()
    {
        // Arrange & Act — an empty key is what the import replaces with the row's position
        var rows = ReadVersions("ImportId,ProductId,Number,Name,TargetDate,CutAt,ReleasedAt,Sequence,Notes\n"
            + ",17f79eeb-d355-4118-9cb0-166b213dd916,1.0.0,Initial release,,,,,\n");

        // Assert
        rows.Single().ImportId.Should().BeEmpty();
    }

    [Fact]
    public void ReadCsv_RefusesABlankBooleanCell()
    {
        // Arrange & Act — a property initializer is not a default for a blank cell: the cell is converted
        var read = () => _csvService.ReadCsv<FlagRow>(new MemoryStream(Encoding.UTF8.GetBytes("Name,IsActive\nFalcon,\n"))).ToList();

        // Assert
        read.Should().Throw<CsvFileException>();
    }

    [CsvRecord]
    internal sealed partial class FlagRow
    {
        public string Name { get; set; } = default!;
        public bool IsActive { get; set; } = true;
    }

    private List<ImportVersionRequest> ReadVersions(string csv) =>
        [.. _csvService.ReadCsv<ImportVersionRequest>(new MemoryStream(Encoding.UTF8.GetBytes(csv)))];

    [Fact]
    public void ReadCsv_RefusesATimestampInADateColumn()
    {
        // Arrange & Act — the column means a date, so a time is a mistake in the file rather than
        // something to throw away
        var read = () => Read("Name,Start,End\nPI 2026.1,2026-01-05T00:00:00Z,\n").ToList();

        // Assert
        read.Should().Throw<CsvFileException>();
    }

    private const string IntakeRequestHeader =
        "ImportId,Title,Description,ExpectedBenefits,DeskId,ChannelId,RequesterId,SubmittedById,SubmittedAt,NeedByDate,NeedByBasis,"
        + "NeedByReference,NeedByCommittedById,NeedByCommittedOn,OriginSourceSystem,OriginExternalId,OriginUrl,Status,StatusChangedAt,"
        + "StatusReason,DuplicateOfId,AssigneeId,TagIds";

    private const string IntakeRequestCells =
        "r1,Export invoices,Finance needs a CSV export,,17f79eeb-d355-4118-9cb0-166b213dd916,,5a3c1e2b-8d47-4f1a-9c6e-2b7d8e9f0a1b,,,,,,,,,,,,,,,,";

    private List<ImportIntakeRequestRequest> ReadIntakeRequests(string csv) =>
        [.. _csvService.ReadCsv<ImportIntakeRequestRequest>(new MemoryStream(Encoding.UTF8.GetBytes(csv)))];

    [Fact]
    public void ReadCsv_ReadsAnIntakeRequestFileWithoutTheOptionalEstimateColumn()
    {
        // Arrange & Act — the one column a file may leave out, so files written before it existed still import
        var rows = ReadIntakeRequests(IntakeRequestHeader + "\n" + IntakeRequestCells + "\n");

        // Assert
        rows.Single().Estimate.Should().BeNull();
    }

    [Fact]
    public void ReadCsv_ReadsTheEstimateColumnWhenPresent()
    {
        // Arrange & Act
        var rows = ReadIntakeRequests(IntakeRequestHeader + ",Estimate\n" + IntakeRequestCells + ",XL\n");

        // Assert
        rows.Single().Estimate.Should().Be("XL");
    }

    [Fact]
    public void ReadCsv_RefusesBrokenQuoting()
    {
        // Arrange & Act — CsvHelper would otherwise repair the cell into a value nobody wrote
        var read = () => Read("Name,Start,End\n\"PI \"2026.1,2026-01-05,\n").ToList();

        // Assert — the parser's exception is kept for the log
        read.Should().Throw<CsvFileException>().WithInnerException<CsvFormatException>();
    }

    [Fact]
    public void ReadCsv_RefusesARowShortOfCells()
    {
        // Arrange & Act
        var read = () => Read("Name,Start,End\nPI 2026.1,2026-01-05\n").ToList();

        // Assert
        read.Should().Throw<CsvFileException>();
    }

    [Fact]
    public void ReadCsv_RefusesASemicolonSeparatedFile()
    {
        // Arrange & Act — the separator is a comma, never guessed from the file
        var read = () => Read("Name;Start;End\nPI 2026.1;2026-01-05;\n").ToList();

        // Assert
        read.Should().Throw<CsvFileException>();
    }

    [Fact]
    public void ReadCsv_ReadsAQuotedCellWithACommaAndAQuote()
    {
        // Arrange & Act
        var rows = Read("Name,Start,End\n\"PI, \"\"one\"\"\",2026-01-05,\n").ToList();

        // Assert
        rows.Single().Name.Should().Be("PI, \"one\"");
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("y")]
    [InlineData("1")]
    public void ReadCsv_RefusesABooleanOtherThanTrueOrFalse(string cell)
    {
        // Arrange & Act — the import pages say true or false, and CsvHelper would otherwise accept more
        var read = () => ReadFlags($"Name,IsActive\nFalcon,{cell}\n");

        // Assert
        read.Should().Throw<CsvFileException>();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("FALSE", false)]
    public void ReadCsv_ReadsTrueAndFalseInAnyCase(string cell, bool expected)
    {
        // Arrange & Act — the data generation tool and spreadsheets write True and False
        var rows = ReadFlags($"Name,IsActive\nFalcon,{cell}\n");

        // Assert
        rows.Single().IsActive.Should().Be(expected);
    }

    [Fact]
    public void ReadCsv_MatchesAHeaderIgnoringCaseAndSurroundingSpaces()
    {
        // Arrange & Act
        var rows = Read("name, START ,End\nPI 2026.1,2026-01-05,\n").ToList();

        // Assert
        var row = rows.Single();
        row.Name.Should().Be("PI 2026.1");
        row.Start.Should().Be(new DateOnly(2026, 1, 5));
    }

    [Fact]
    public void ReadCsv_SkipsTheSeparatorLineExcelWrites()
    {
        // Arrange & Act
        var rows = Read("sep=,\nName,Start,End\nPI 2026.1,2026-01-05,\n").ToList();

        // Assert
        rows.Single().Name.Should().Be("PI 2026.1");
    }

    [Fact]
    public void ReadCsv_RefusesAnEmptyFile()
    {
        // Arrange & Act
        var read = () => Read(string.Empty).ToList();

        // Assert
        read.Should().Throw<CsvFileException>();
    }

    [Fact]
    public void ReadCsv_IgnoresCellsBeyondTheHeader()
    {
        // Arrange & Act
        var rows = Read("Name,Start,End\nPI 2026.1,2026-01-05,,stray\n").ToList();

        // Assert
        rows.Single().End.Should().BeNull();
    }

    [Fact]
    public void ReadCsv_IgnoresAColumnTheRowDoesNotHave()
    {
        // Arrange & Act — a file exported with an extra column still imports
        var rows = Read("Name,Notes,Start,End\nPI 2026.1,carried over,2026-01-05,\n").ToList();

        // Assert
        var row = rows.Single();
        row.Name.Should().Be("PI 2026.1");
        row.Start.Should().Be(new DateOnly(2026, 1, 5));
    }

    [Fact]
    public void ReadCsv_KeepsATimestampMarkedUtcInUtc()
    {
        // Arrange & Act — not moved to the server's time zone, which would shift every *Utc column on a server
        // that does not run in UTC
        var rows = _csvService.ReadCsv<TimestampRow>(new MemoryStream(Encoding.UTF8.GetBytes("At\n2026-01-05T10:00:00Z\n"))).ToList();

        // Assert
        rows.Single().At.Should().Be(new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc));
        rows.Single().At.Kind.Should().Be(DateTimeKind.Utc);
    }

    [CsvRecord]
    internal sealed partial class TimestampRow
    {
        public DateTime At { get; set; }
    }

    private List<FlagRow> ReadFlags(string csv) =>
        [.. _csvService.ReadCsv<FlagRow>(new MemoryStream(Encoding.UTF8.GetBytes(csv)))];

    public static TheoryData<Type> ImportRowTypes()
    {
        var types = typeof(CsvService).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters())
            .Select(p => p.GetCustomAttribute<CsvRowsAttribute>()?.RowType)
            .OfType<Type>()
            .Distinct();

        return [.. types];
    }

    [Theory]
    [MemberData(nameof(ImportRowTypes))]
    public void ReadCsv_ReadsEveryImportRowTypeFromItsPublishedColumns(Type rowType)
    {
        // Arrange — the columns the import template publishes: every public settable property
        var header = string.Join(',', rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name));
        var read = typeof(CsvService).GetMethod(nameof(CsvService.ReadCsv))!.MakeGenericMethod(rowType);

        // Act — a type without a generated mapper, or one requiring a column the template leaves out, throws
        var rows = (IEnumerable<object>)read.Invoke(_csvService, [new MemoryStream(Encoding.UTF8.GetBytes(header + "\n"))])!;

        // Assert
        rows.Should().BeEmpty();
    }
}
