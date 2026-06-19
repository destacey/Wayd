namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

public sealed record ProjectTaskTypeDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
}
