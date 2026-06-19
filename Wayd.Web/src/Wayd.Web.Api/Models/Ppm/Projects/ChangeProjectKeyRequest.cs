using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

namespace Wayd.Web.Api.Models.Ppm.Projects;

public sealed record ChangeProjectKeyRequest
{
    /// <summary>
    /// The new key to assign to the Project (2-20 uppercase alphanumeric characters).
    /// </summary>
    public string Key { get; set; } = default!;

    public ChangeProjectKeyCommand ToChangeProjectKeyCommand(Guid id)
        => new ChangeProjectKeyCommand(id, new ProjectKey(Key));
}

public sealed class ChangeProjectKeyRequestValidator : CustomValidator<ChangeProjectKeyRequest>
{
    public ChangeProjectKeyRequestValidator()
    {
        RuleFor(p => p.Key)
            .NotNull()
            .Matches(ProjectKey.Regex)
                .WithMessage("Invalid code format. Project keys are uppercase letters and numbers only, 2-20 characters.");
    }
}
