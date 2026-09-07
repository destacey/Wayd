using System.Reflection;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Imports;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Imports;
using Wayd.ProjectPortfolioManagement.Application.Programs.Imports;
using Wayd.ProjectPortfolioManagement.Application.Projects.Imports;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Imports;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Imports;

namespace Wayd.ProjectPortfolioManagement.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddProjectPortfolioManagementApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IImportDefinition, ProjectPortfolioImportDefinition>();
        services.AddScoped<IImportDefinition, ProgramImportDefinition>();
        services.AddScoped<IImportDefinition, ProjectImportDefinition>();
        services.AddScoped<IImportDefinition, ProjectStageImportDefinition>();
        services.AddScoped<IImportDefinition, ProjectTaskImportDefinition>();
        services.AddScoped<IImportDefinition, StrategicInitiativeImportDefinition>();
        services.AddScoped<IImportDefinition, PpmFinalizationImportDefinition>();

        ConfigureMapster(assembly);

        return services;
    }
    private static void ConfigureMapster(Assembly assembly)
    {
        // Global Mapster settings
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        TypeAdapterConfig.GlobalSettings.Default.PreserveReference(true);
        TypeAdapterConfig.GlobalSettings.AllowImplicitSourceInheritance = true;
        TypeAdapterConfig.GlobalSettings.AllowImplicitDestinationInheritance = true;
    }
}