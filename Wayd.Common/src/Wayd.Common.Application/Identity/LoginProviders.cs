namespace Wayd.Common.Application.Identity;

public static class LoginProviders
{
    public const string MicrosoftEntraId = "MicrosoftEntraId";
    public const string Wayd = "Wayd";

    public static readonly string[] All = [MicrosoftEntraId, Wayd];
}
