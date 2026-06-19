namespace Wayd.Web.Api.Models.Admin;

public class ToggleFeatureFlagRequest
{
    public int Id { get; set; }
    public bool IsEnabled { get; set; }
}
