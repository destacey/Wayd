using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Wayd.Infrastructure.Logging;
using Wolverine;

namespace Wayd.Infrastructure.Tests.Sut.Logging;

public sealed class PayloadPropertyFilterTests
{
    [Theory]
    [InlineData(nameof(PayloadException.Envelope))]
    [InlineData(nameof(PayloadException.EnvelopeArray))]
    [InlineData(nameof(PayloadException.EnvelopeList))]
    [InlineData(nameof(PayloadException.Entry))]
    [InlineData(nameof(PayloadException.Entries))]
    [InlineData(nameof(PayloadException.Context))]
    public void ShouldPropertyBeFiltered_IsTrue_ForAPayloadOrACollectionOfThem(string propertyName)
    {
        // Arrange
        var filter = new PayloadPropertyFilter();

        // Act
        var filtered = filter.ShouldPropertyBeFiltered(new PayloadException(), propertyName, "destructured");

        // Assert
        filtered.Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(PayloadException.Reason))]
    [InlineData(nameof(PayloadException.Attempts))]
    [InlineData(nameof(PayloadException.Names))]
    [InlineData(nameof(Exception.Message))]
    [InlineData("NotAProperty")]
    public void ShouldPropertyBeFiltered_IsFalse_ForOtherProperties(string propertyName)
    {
        // Arrange
        var filter = new PayloadPropertyFilter();

        // Act
        var filtered = filter.ShouldPropertyBeFiltered(new PayloadException(), propertyName, "destructured");

        // Assert
        filtered.Should().BeFalse();
    }

    [Fact]
    public void ShouldPropertyBeFiltered_IsFalse_ForTheEntriesOfAFailedSave()
    {
        // Arrange
        var filter = new PayloadPropertyFilter();

        // Act
        var filtered = filter.ShouldPropertyBeFiltered(
            new DbUpdateException("save failed"), nameof(DbUpdateException.Entries), "destructured");

        // Assert
        filtered.Should().BeFalse();
    }

    private sealed class PayloadException : Exception
    {
        public Envelope? Envelope => null;

        public Envelope[] EnvelopeArray => [];

        public IReadOnlyList<Envelope> EnvelopeList => [];

        public EntityEntry? Entry => null;

        public IEnumerable<EntityEntry> Entries => [];

        public DbContext? Context => null;

        public string Reason => "failed";

        public int Attempts => 1;

        public List<string> Names => [];
    }
}
