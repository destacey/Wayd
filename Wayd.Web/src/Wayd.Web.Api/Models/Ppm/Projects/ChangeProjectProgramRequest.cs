using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

namespace Wayd.Web.Api.Models.Ppm.Projects;

public sealed record ChangeProjectProgramRequest
{
    /// <summary>
    /// The new ProgramId to assign to the Project. If null the Program will be removed.
    /// </summary>
    public Guid? ProgramId { get; set; }

    public ChangeProjectProgramCommand ToChangeProjectProgramCommand(Guid id)
        => new ChangeProjectProgramCommand(id, ProgramId);
}
