namespace Wayd.Common.Application.Identity.Roles;

public sealed record CreateOrUpdateRoleCommand
{
    public CreateOrUpdateRoleCommand(string? id, string name, string? description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public string? Id { get; }
    public string Name { get; }
    public string? Description { get; }
}

public sealed class CreateOrUpdateRoleCommandValidator : CustomValidator<CreateOrUpdateRoleCommand>
{
    public CreateOrUpdateRoleCommandValidator(IRoleService roleService) =>
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(256)
            .MustAsync(async (role, name, _) => !await roleService.Exists(name, role.Id))
                .WithMessage("Similar Role already exists.");
}