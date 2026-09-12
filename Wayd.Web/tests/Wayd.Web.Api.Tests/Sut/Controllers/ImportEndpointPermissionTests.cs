using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wayd.Common.Application.Employees.Imports;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Planning.Application.PlanningIntervals.Imports;
using Wayd.Planning.Application.Risks.Imports;
using Wayd.ProductManagement.Application.Products.Imports;
using Wayd.ProductManagement.Application.ReleasePackages.Imports;
using Wayd.ProductManagement.Application.Releases.Imports;
using Wayd.ProductManagement.Application.Versions.Imports;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Imports;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Imports;
using Wayd.ProjectPortfolioManagement.Application.Programs.Imports;
using Wayd.ProjectPortfolioManagement.Application.Projects.Imports;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Imports;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Imports;
using Wayd.StrategicManagement.Application.StrategicThemes.Imports;
using Wayd.Web.Api.Controllers.Organizations;
using Wayd.Web.Api.Controllers.Planning;
using Wayd.Web.Api.Controllers.Ppm;
using Wayd.Web.Api.Controllers.ProductManagement;
using Wayd.Web.Api.Controllers.StrategicManagement;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.Tests.Sut.Controllers;

/// <summary>
/// Holds each import endpoint's permission to the one its import definition declares.
/// </summary>
/// <remarks>
/// The endpoint's attribute gates submitting a file; the definition's permission gates reading the run
/// back. An endpoint answers with the run it just created, read as the submitter — so if the two differ,
/// a submission can succeed and then answer 400, and the submitter cannot find the run afterwards either.
/// </remarks>
public sealed class ImportEndpointPermissionTests
{
    private static readonly (Type Controller, string Method, Type Definition)[] _endpoints =
    [
        (typeof(EmployeesController), nameof(EmployeesController.Import), typeof(EmployeeImportDefinition)),
        (typeof(TeamsController), nameof(TeamsController.Import), typeof(TeamImportDefinition)),
        (typeof(TeamsController), nameof(TeamsController.ImportMembers), typeof(TeamMemberImportDefinition)),
        (typeof(TeamsController), nameof(TeamsController.ImportTeamMemberships), typeof(TeamMembershipImportDefinition)),
        (typeof(PlanningIntervalsController), nameof(PlanningIntervalsController.ImportObjectives), typeof(PlanningIntervalObjectiveImportDefinition)),
        (typeof(RisksController), nameof(RisksController.Import), typeof(RiskImportDefinition)),
        (typeof(PortfoliosController), nameof(PortfoliosController.Import), typeof(ProjectPortfolioImportDefinition)),
        (typeof(PortfoliosController), nameof(PortfoliosController.FinalizeImport), typeof(PpmFinalizationImportDefinition)),
        (typeof(ProgramsController), nameof(ProgramsController.Import), typeof(ProgramImportDefinition)),
        (typeof(ProjectsController), nameof(ProjectsController.Import), typeof(ProjectImportDefinition)),
        (typeof(ProjectsController), nameof(ProjectsController.ImportTasks), typeof(ProjectTaskImportDefinition)),
        (typeof(ProjectsController), nameof(ProjectsController.ImportStages), typeof(ProjectStageImportDefinition)),
        (typeof(StrategicInitiativesController), nameof(StrategicInitiativesController.Import), typeof(StrategicInitiativeImportDefinition)),
        (typeof(ProductsController), nameof(ProductsController.Import), typeof(ProductImportDefinition)),
        (typeof(ReleasePackagesController), nameof(ReleasePackagesController.Import), typeof(ReleasePackageImportDefinition)),
        (typeof(ReleasesController), nameof(ReleasesController.Import), typeof(ReleaseImportDefinition)),
        (typeof(VersionsController), nameof(VersionsController.Import), typeof(VersionImportDefinition)),
        (typeof(StrategicThemesController), nameof(StrategicThemesController.Import), typeof(StrategicThemeImportDefinition)),
    ];

    public static TheoryData<Type, string, Type> Endpoints =>
        new(_endpoints.Select(e => (e.Controller, e.Method, e.Definition)));

    /// <summary>
    /// Every action that takes an uploaded file. Found by the file rather than the responder, so an import
    /// endpoint that forgot the responder is still found — and then fails the check that it has one.
    /// </summary>
    private static IEnumerable<MethodInfo> ImportEndpoints() =>
        typeof(ImportSubmissionResponder).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(IFormFile)));

    [Theory]
    [MemberData(nameof(Endpoints))]
    public void Endpoint_RequiresThePermissionItsImportDefinitionDeclares(Type controller, string method, Type definitionType)
    {
        // Arrange — the permission members are constants, so no constructor dependencies are needed
        var definition = (IImportDefinition)RuntimeHelpers.GetUninitializedObject(definitionType);
        var expected = ApplicationPermission.NameFor(definition.PermissionAction, definition.PermissionResource);

        // Act
        var policy = controller.GetMethod(method)!.GetCustomAttribute<MustHavePermissionAttribute>()?.Policy;

        // Assert
        policy.Should().Be(expected);
    }

    [Fact]
    public void Endpoints_ListsEveryImportEndpoint()
    {
        // Arrange
        var listed = _endpoints.Select(e => $"{e.Controller.Name}.{e.Method}");

        // Act
        var actual = ImportEndpoints().Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        // Assert — a new import endpoint has to be added above, or its permission goes unchecked
        actual.Should().BeEquivalentTo(listed);
    }

    [Fact]
    public void Endpoints_AnswerWithTheRunThroughTheResponder()
    {
        // Arrange & Act
        var withoutResponder = ImportEndpoints()
            .Where(m => m.GetParameters().All(p => p.ParameterType != typeof(ImportSubmissionResponder)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        // Assert — without it an endpoint answers with a bare id, and its submitter gets no outcome
        withoutResponder.Should().BeEmpty();
    }

    [Fact]
    public void Endpoints_TakeTheSubmissionGroupFromTheQueryString()
    {
        // Arrange & Act — the same parameter on every endpoint, so a tool posting a set of files can
        // group them without knowing which endpoint it is talking to. From the query, not the form: a
        // second [FromForm] beside the IFormFile makes the OpenAPI description expand the file into its
        // own properties, and every generated client loses the "file" field.
        var withoutGroup = ImportEndpoints()
            .Where(m => !m.GetParameters().Any(p =>
                p.Name == "submissionGroupId"
                && p.ParameterType == typeof(Guid?)
                && p.GetCustomAttribute<FromQueryAttribute>() is not null))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        // Assert
        withoutGroup.Should().BeEmpty();
    }
}
