using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;

namespace Wayd.Web.Api.Models.Ppm.Portfolios;

public sealed record UpdatePortfolioRequest
{
    /// <summary>
    /// The unique identifier of the portfolio.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the portfolio.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// A detailed description of the portfolio’s purpose.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// The sponsors of the portfolio.
    /// </summary>
    public List<Guid>? SponsorIds { get; set; } = [];

    /// <summary>
    /// The owners of the portfolio.
    /// </summary>
    public List<Guid>? OwnerIds { get; set; } = [];

    /// <summary>
    /// The managers of the portfolio.
    /// </summary>
    public List<Guid>? ManagerIds { get; set; } = [];

    public UpdateProjectPortfolioCommand ToUpdateProjectPortfolioCommand()
    {
        return new UpdateProjectPortfolioCommand(Id, Name, Description, SponsorIds, OwnerIds, ManagerIds);
    }

    /// <summary>
    /// The update that would leave <paramref name="dto"/> exactly as it is: the record a JSON Patch is applied to.
    /// </summary>
    public static UpdatePortfolioRequest FromDto(ProjectPortfolioDetailsDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        SponsorIds = [.. dto.PortfolioSponsors.Select(e => e.Id)],
        OwnerIds = [.. dto.PortfolioOwners.Select(e => e.Id)],
        ManagerIds = [.. dto.PortfolioManagers.Select(e => e.Id)],
    };
}

public sealed class UpdateProjectPortfolioRequestValidator : CustomValidator<UpdatePortfolioRequest>
{
    public UpdateProjectPortfolioRequestValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}
