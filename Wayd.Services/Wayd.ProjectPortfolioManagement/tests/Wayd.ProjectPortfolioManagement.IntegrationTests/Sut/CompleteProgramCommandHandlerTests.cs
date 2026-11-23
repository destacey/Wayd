using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Programs.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="CompleteProgramCommandHandler"/> against a real SQL Server container.
/// <para>
/// A program refuses to complete while a project is open, and it can only see its projects if the handler includes
/// them; left out, the collection reads as empty and the program completes. The in-memory fake treats
/// <c>.Include</c> as a no-op, so only a real database shows the difference.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class CompleteProgramCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public CompleteProgramCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WithAnOpenProject_RefusesAndLeavesTheProgramActive()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActivePortfolioWithProgram.Seed(_fixture, cancelTheProject: false, cancelTheProgram: false, ct);

        // Act
        var result = await Complete(seed, ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All projects must be completed or canceled before the program can be completed.");

        await using var verify = _fixture.CreateContext();
        (await verify.Programs.SingleAsync(p => p.Id == seed.ProgramId, ct)).Status.Should().Be(ProgramStatus.Active);
    }

    [Fact]
    public async Task Handle_WithEveryProjectClosed_CompletesTheProgram()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActivePortfolioWithProgram.Seed(_fixture, cancelTheProject: true, cancelTheProgram: false, ct);

        // Act
        var result = await Complete(seed, ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.Programs.SingleAsync(p => p.Id == seed.ProgramId, ct)).Status.Should().Be(ProgramStatus.Completed);
    }

    private async Task<Result> Complete(ActivePortfolioWithProgram seed, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await Handler(context, seed.EmployeeId).Handle(new CompleteProgramCommand(seed.ProgramId), ct);
    }

    private static CompleteProgramCommandHandler Handler(WaydDbContext context, Guid employeeId) =>
        new(
            context,
            PpmAdministrator.Principal(employeeId),
            PpmAdministrator.User(),
            Mock.Of<ILogger<CompleteProgramCommandHandler>>(),
            ActiveProjectWithDeliverable.Clock());
}
