using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Recipes;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>Posts a generated dataset into a Wayd environment.</summary>
/// <param name="dataset">What to post.</param>
/// <param name="resolved">The recipe it was generated from, which carries the settings a seed needs beyond the data.</param>
/// <param name="apiUrl">The API's base URL.</param>
/// <param name="apiKey">The Personal Access Token every request is sent with.</param>
/// <param name="log">Where progress is written.</param>
public delegate Task SeedExecutor(
    GeneratedDataset dataset,
    ResolvedRecipe resolved,
    string apiUrl,
    string apiKey,
    Action<string> log,
    CancellationToken cancellationToken);

/// <summary>
/// The one way a dataset reaches an environment, shared by the <c>seed</c> verb and the page.
/// </summary>
public static class DatasetSeeder
{
    public static async Task Seed(
        GeneratedDataset dataset,
        ResolvedRecipe resolved,
        string apiUrl,
        string apiKey,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        // One group per seed run, so the files it posts can be found together afterwards.
        using var client = new WaydSeedClient(apiUrl, apiKey, submissionGroupId: Guid.NewGuid());

        await new SeedRunner(client, log).Run(
            dataset.Org,
            dataset.Ppm,
            dataset.ProductManagement,
            dataset.Planning,
            resolved.CreateUsers,
            resolved.UserPassword,
            cancellationToken);
    }
}
