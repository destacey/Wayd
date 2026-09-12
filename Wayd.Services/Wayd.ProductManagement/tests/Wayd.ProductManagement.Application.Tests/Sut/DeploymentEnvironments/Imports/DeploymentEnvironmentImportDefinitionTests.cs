using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Imports;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Imports;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.DeploymentEnvironments.Imports;

/// <summary>
/// Importing deployment environments. The definition's own work is refusing a name that is already taken
/// and retiring the rows a backfill marks inactive.
/// </summary>
public sealed class DeploymentEnvironmentImportDefinitionTests
{
    private const int CreatePass = 0;

    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly DeploymentEnvironmentImportDefinition _definition;

    public DeploymentEnvironmentImportDefinitionTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns(_userId);
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        _definition = new DeploymentEnvironmentImportDefinition(
            _dbContext,
            _currentUser.Object,
            _dateTimeProvider.Object,
            new ImportPayloadSerializer());
    }

    private ImportProcessRow[] Rows(params ImportDeploymentEnvironmentDto[] environments) =>
        [.. environments.Select((e, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(e)))];

    private Task<Result<ImportPassResult>> Run(params ImportDeploymentEnvironmentDto[] environments) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(environments), isFinalChunk: true, TestContext.Current.CancellationToken);

    private static ImportDeploymentEnvironmentDto Row(
        string name = "Production",
        EnvironmentCategory category = EnvironmentCategory.Production,
        int ringOrder = 0,
        bool isActive = true) =>
        new(name, category, ringOrder, isActive);

    [Fact]
    public void Definition_IsAtomic()
    {
        // Arrange & Act & Assert
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreateEnvironments");
    }

    [Fact]
    public async Task CreateEnvironments_CreatesEveryRowInTheFile()
    {
        // Arrange & Act
        var result = await Run(
            Row("Development", EnvironmentCategory.Development, 0),
            Row("Staging", EnvironmentCategory.Staging, 1),
            Row("Production", EnvironmentCategory.Production, 2));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.DeploymentEnvironments.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEnvironments_RecordsWhatEachRowSaid()
    {
        // Arrange & Act
        var result = await Run(Row("prod-eu", EnvironmentCategory.Production, 3));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var environment = _dbContext.DeploymentEnvironments.Single();
        environment.Name.Should().Be("prod-eu");
        environment.Category.Should().Be(EnvironmentCategory.Production);
        environment.RingOrder.Should().Be(3);
        environment.IsActive.Should().BeTrue();
        outcome.CreatedEntityId.Should().Be(environment.Id);
    }

    [Fact]
    public async Task CreateEnvironments_RetiresARowMarkedInactive()
    {
        // Arrange — through the real transition, so the retirement is recorded the way one made by hand
        // would be
        // Act
        var result = await Run(Row("QA2", EnvironmentCategory.Testing, isActive: false));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var environment = _dbContext.DeploymentEnvironments.Single();
        environment.IsActive.Should().BeFalse();

        var retired = environment.DomainEvents.OfType<EnvironmentRetiredEvent>().Single();
        retired.Actor.Kind.Should().Be(EventActorKind.Import);
        retired.Actor.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task CreateEnvironments_RejectsARowWhoseNameAlreadyExists()
    {
        // Arrange — names are what the deployments import resolves against, so a second environment with
        // the same name would make that resolution ambiguous
        _dbContext.AddDeploymentEnvironment(DeploymentEnvironment.Create(
            "Production", EnvironmentCategory.Production, 0, EventActor.System, Now));

        // Act
        var result = await Run(Row("Production"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Production");
        _dbContext.DeploymentEnvironments.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateEnvironments_TrimsANameSurroundedByWhitespace()
    {
        // Arrange & Act
        var result = await Run(Row(" Staging ", EnvironmentCategory.Staging));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.DeploymentEnvironments.Single().Name.Should().Be("Staging");
    }
}
