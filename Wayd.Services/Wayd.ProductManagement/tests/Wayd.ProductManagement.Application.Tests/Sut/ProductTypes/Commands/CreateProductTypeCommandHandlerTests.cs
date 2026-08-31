using FluentAssertions;
using Wayd.ProductManagement.Application.ProductTypes.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTypes.Commands;

/// <summary>
/// Adding a type to the catalog.
/// </summary>
public sealed class CreateProductTypeCommandHandlerTests : ProductCommandTestBase
{
    private CreateProductTypeCommandHandler CreateSut() =>
        new(DbContext, Logger<CreateProductTypeCommandHandler>());

    [Fact]
    public async Task Create_ShouldAddTheType()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateProductTypeCommand("Service", "A running service.", true, 5),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = DbContext.ProductTypes.Should().ContainSingle().Subject;
        created.Name.Should().Be("Service");
        created.IsReleasable.Should().BeTrue();
        created.IsSystem.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ShouldFail_OnADuplicateName()
    {
        // Arrange
        SeedType("Service");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateProductTypeCommand("Service", null, true, 5), TestContext.Current.CancellationToken);

        // Assert
        // Checked in the handler as well as by the unique index, so a duplicate is a readable message
        // rather than a DbUpdateException.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A product type named 'Service' already exists.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
