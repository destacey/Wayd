namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The generated Product Management dataset, keyed throughout by the generator's own handles. Each seed area
/// projects its slice to CSV rows, substituting the ids the environment has handed back by then.
/// </summary>
public sealed record GeneratedProductManagement(
    IReadOnlyList<DeploymentEnvironmentModel> Environments,
    IReadOnlyList<ProductModel> Products,
    IReadOnlyList<VersionModel> Versions,
    IReadOnlyList<ReleasePackageModel> ReleasePackages,
    IReadOnlyList<ReleasePackageComponentModel> ReleasePackageComponents,
    IReadOnlyList<ReleaseModel> Releases,
    IReadOnlyList<ReleaseContentModel> ReleaseContents,
    IReadOnlyList<DeploymentModel> Deployments);
