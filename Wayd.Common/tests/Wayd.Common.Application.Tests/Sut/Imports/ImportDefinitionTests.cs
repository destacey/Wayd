using FluentAssertions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class ImportDefinitionTests
{
    private static readonly TestSerializerService _serializer = new();

    private static TestImportDefinition CreateDefinition() => new(_serializer);

    private static ImportProcessRow Row(TestImportDefinition definition, string importId, int rowNumber, bool shouldFail = false) =>
        ImportProcessRow.Create(importId, rowNumber, definition.SerializeRow(new TestImportRow($"Row {rowNumber}", shouldFail)));

    [Fact]
    public void Passes_ExposesTheStepsInOrderWithTheirScope()
    {
        // Arrange & Act
        var passes = CreateDefinition().Passes;

        // Assert
        passes.Select(p => p.Name).Should().Equal("Create", "Link");
        passes[0].Scope.Should().Be(ImportPassScope.Chunked);
        passes[1].Scope.Should().Be(ImportPassScope.WholeSet);
    }

    [Fact]
    public async Task ExecutePass_HandsThePassItsRowsDeserialized()
    {
        // Arrange
        var definition = CreateDefinition();
        var rows = new[] { Row(definition, "a", 1), Row(definition, "b", 2) };

        // Act
        var result = await definition.ExecutePass(Guid.CreateVersion7(), 0, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        definition.Calls.Should().ContainSingle();
        definition.Calls[0].Pass.Should().Be("Create");
        definition.Calls[0].ImportIds.Should().Equal("a", "b");
        definition.Calls[0].IsFinalChunk.Should().BeTrue();
    }

    [Fact]
    public async Task ExecutePass_ReportsTheCreatedRecordAgainstTheCallersKey()
    {
        // Arrange
        var definition = CreateDefinition();
        var rows = new[] { Row(definition, "emp-1", 1) };

        // Act
        var result = await definition.ExecutePass(Guid.CreateVersion7(), 0, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

        // Assert
        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.ImportId.Should().Be("emp-1");
        row.Failed.Should().BeFalse();
        row.CreatedEntityId.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecutePass_ReportsARejectedRowWithoutFailingThePass()
    {
        // Arrange — one good row, one the pass rejects
        var definition = CreateDefinition();
        var rows = new[] { Row(definition, "good", 1), Row(definition, "bad", 2, shouldFail: true) };

        // Act
        var result = await definition.ExecutePass(Guid.CreateVersion7(), 0, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

        // Assert — the run continues; only the row is rejected
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Single(r => r.ImportId == "good").Failed.Should().BeFalse();

        var bad = result.Value.Rows.Single(r => r.ImportId == "bad");
        bad.Failed.Should().BeTrue();
        bad.Error.Should().Contain("bad");
        bad.CreatedEntityId.Should().BeNull();
    }

    [Fact]
    public async Task ExecutePass_FailsTheWholeCallWhenThePassItselfCannotRun()
    {
        // Arrange
        var definition = CreateDefinition();
        definition.PassFailure = "The lookup table is missing.";
        var rows = new[] { Row(definition, "a", 1) };

        // Act
        var result = await definition.ExecutePass(Guid.CreateVersion7(), 0, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

        // Assert — distinct from a row failing: this stops the attempt
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The lookup table is missing.");
    }

    [Fact]
    public async Task ExecutePass_RefusesARowTheRetentionSweepHasEmptied()
    {
        // Arrange
        var definition = CreateDefinition();
        var row = Row(definition, "a", 1);
        row.PurgePayload();

        // Act
        var result = await definition.ExecutePass(Guid.CreateVersion7(), 0, [row], isFinalChunk: true, TestContext.Current.CancellationToken);

        // Assert — a clear message rather than a deserialization failure
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("retention");
    }

    [Fact]
    public async Task ExecutePass_RejectsAPassIndexThatDoesNotExist()
    {
        // Arrange
        var definition = CreateDefinition();
        var rows = new[] { Row(definition, "a", 1) };

        // Act
        var result = await definition.ExecutePass(Guid.CreateVersion7(), 5, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no pass at index 5");
    }

    [Fact]
    public void Defaults_AreDeclaredPerImportRatherThanAssumed()
    {
        // Arrange & Act
        var definition = CreateDefinition();

        // Assert — the definition overrode the chunk size; the rest fall back to the base
        definition.ChunkSize.Should().Be(2);
        definition.InlineThreshold.Should().Be(100);
        definition.MaxRows.Should().Be(50_000);
        definition.Atomicity.Should().Be(Common.Domain.Enums.Imports.ImportAtomicity.PerRow);
    }
}
