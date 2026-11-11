namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>A project as a link: enough to name it and open it by key.</summary>
public sealed record ProjectNavigationDto
{
    /// <summary>The project's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The project's key, such as GEMINI, which its page is addressed by.</summary>
    public required string Key { get; init; }

    /// <summary>The project's name.</summary>
    public required string Name { get; init; }
}
