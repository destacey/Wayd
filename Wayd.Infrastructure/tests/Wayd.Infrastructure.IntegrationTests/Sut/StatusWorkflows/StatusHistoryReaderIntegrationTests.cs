using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// Reads status history through a real provider.
/// </summary>
/// <remarks>
/// The handler tests run against in-memory fakes, where LINQ executes as objects: a predicate that
/// SQL Server cannot translate still passes, and so does one joining a table the query never loads.
/// These cover what only a database can answer — that the read translates at all, that it scopes to
/// one record, and that the employee frozen onto a transition survives the round trip and resolves.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class StatusHistoryReaderIntegrationTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant Timestamp = Instant.FromUtc(2026, 3, 4, 10, 0, 0);

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Timestamp);
        provider.SetupGet(d => d.Today).Returns(new LocalDate(2026, 3, 4));

        return provider.Object;
    }

    private static StatusHistoryReader CreateSut(WaydDbContext context) => new(context, context);

    private static StatusRef StatusOf(StatusWorkflow workflow, int index) =>
        StatusRef.From(workflow.Statuses.OrderBy(s => s.Order).Skip(index).First());

    private async Task<StatusWorkflow> ProductWorkflow(WaydDbContext context)
    {
        ProductWorkflowOwners.Register();

        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(
                w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem,
                TestContext.Current.CancellationToken);

        if (workflow is null)
        {
            await new ProductManagementWorkflowSeeder()
                .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

            workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(
                    w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem,
                    TestContext.Current.CancellationToken);
        }

        return workflow;
    }

    private async Task<Guid> SeedProductType(WaydDbContext context)
    {
        var existing = await context.ProductTypes
            .Select(t => t.Id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        if (existing != Guid.Empty)
        {
            return existing;
        }

        await new ProductTypeSeeder()
            .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

        return await context.ProductTypes
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Employee> SeedEmployee(WaydDbContext context, string firstName, string lastName)
    {
        var employee = Employee.Create(
            new PersonName(firstName, null, lastName),
            $"{Guid.CreateVersion7():N}"[..12],
            Timestamp,
            new EmailAddress($"{Guid.CreateVersion7():N}"[..12] + "@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            Timestamp);

        context.Employees.Add(employee);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee;
    }

    private async Task<Product> SeedProduct(WaydDbContext context, StatusWorkflow workflow, EventActor actor)
    {
        var productType = await SeedProductType(context);

        var product = Product.Create(
            $"History {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            StatusOf(workflow, 0),
            actor,
            Timestamp);

        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return product;
    }

    [Fact]
    public async Task Read_ShouldTranslateAndReturnTheHistoryNewestFirst()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var workflow = await ProductWorkflow(context);
        var product = await SeedProduct(context, workflow, EventActor.System);

        var second = StatusOf(workflow, 1);
        product.ChangeStatus(second, EventActor.System, Timestamp.Plus(Duration.FromHours(1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await using var reader = _fixture.CreateContext();
        var result = await CreateSut(reader).Read(
            ProductWorkflowOwners.Product.Key, product.Id, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(t => t.Sequence).Should().ContainInOrder(1, 0);
        result.Value[0].ToStatus.Name.Should().Be(second.Name);
        result.Value[1].FromStatus.Should().BeNull("the opening transition has nothing to come from");
    }

    [Fact]
    public async Task Read_ShouldReturnOnlyTheRequestedRecord()
    {
        // Arrange — two products, so a predicate that dropped the record filter would return both.
        await using var context = _fixture.CreateContext();
        var workflow = await ProductWorkflow(context);
        var product = await SeedProduct(context, workflow, EventActor.System);
        var other = await SeedProduct(context, workflow, EventActor.System);

        // Act
        await using var reader = _fixture.CreateContext();
        var result = await CreateSut(reader).Read(
            ProductWorkflowOwners.Product.Key, product.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value.Should().OnlyContain(t => t.Sequence == 0);
        other.Id.Should().NotBe(product.Id);
    }

    [Fact]
    public async Task Read_ShouldResolveTheEmployeeFrozenOntoTheTransition()
    {
        // Arrange — the whole point of freezing ActorEmployeeId: it round-trips as a real column and
        // the read resolves it without following the account's current link.
        await using var context = _fixture.CreateContext();
        var workflow = await ProductWorkflow(context);
        var employee = await SeedEmployee(context, "Ada", "Lovelace");
        var product = await SeedProduct(
            context, workflow, EventActor.User(Guid.CreateVersion7().ToString(), employee.Id));

        // Act
        await using var reader = _fixture.CreateContext();
        var result = await CreateSut(reader).Read(
            ProductWorkflowOwners.Product.Key, product.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].ChangedBy.Should().NotBeNull();
        result.Value[0].ChangedBy!.Id.Should().Be(employee.Id);
        result.Value[0].ChangedBy!.Name.Should().Be("Ada Lovelace");
    }

    [Fact]
    public async Task Read_ShouldKeepATransitionWhoseAccountIsNotAUserRow()
    {
        // Arrange — the account id belongs to no user row, as it would after a deletion. An inner join
        // would drop the transition; the read must still report the change.
        await using var context = _fixture.CreateContext();
        var workflow = await ProductWorkflow(context);
        var product = await SeedProduct(
            context, workflow, EventActor.User(Guid.CreateVersion7().ToString()));

        // Act
        await using var reader = _fixture.CreateContext();
        var result = await CreateSut(reader).Read(
            ProductWorkflowOwners.Product.Key, product.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].ChangedByUser.Should().BeNull();
        result.Value[0].ChangedBy.Should().BeNull();
        result.Value[0].ChangedBySystem.Should().BeFalse("a missing account is not the platform acting");
    }

    [Fact]
    public async Task Read_ShouldReturnEmptyForARecordWithNoHistory()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        await ProductWorkflow(context);

        // Act
        var result = await CreateSut(context).Read(
            ProductWorkflowOwners.Product.Key, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
