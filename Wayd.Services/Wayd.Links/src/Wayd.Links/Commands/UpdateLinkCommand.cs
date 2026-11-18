using CSharpFunctionalExtensions;
using FluentValidation;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Application.Validation;
using Wayd.Common.Domain.Links;

namespace Wayd.Links.Commands;

/// <summary>
/// Changes a link's name and address. Only someone who may edit the record the link is on.
/// </summary>
/// <param name="OwnerType">The record's registered owner type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="LinkId">The link's id.</param>
/// <param name="Name">What the link is called.</param>
/// <param name="Url">The web address the link opens.</param>
public sealed record UpdateLinkCommand(string OwnerType, Guid OwnerId, Guid LinkId, string Name, string Url) : ICommand;

/// <summary>Rejects malformed input before the record is resolved.</summary>
/// <remarks>
/// The address is only required here: whether it is a web address is the link's own rule, which lets a link saved
/// before addresses were checked keep the one it has.
/// </remarks>
public sealed class UpdateLinkCommandValidator : CustomValidator<UpdateLinkCommand>
{
    public UpdateLinkCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.OwnerType).NotEmpty().MaximumLength(LinkOwnerTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
        RuleFor(c => c.LinkId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Link.NameMaxLength);
        RuleFor(c => c.Url).NotEmpty();
    }
}

/// <summary>Changes the link after the record's owner type allows it, and saves.</summary>
public sealed class UpdateLinkCommandHandler(
    LinkOwnerAccessResolver accessResolver,
    ILinkWriter linkWriter,
    ILinksDbContext linksDbContext) : ICommandHandler<UpdateLinkCommand>
{
    private readonly LinkOwnerAccessResolver _accessResolver = accessResolver;
    private readonly ILinkWriter _linkWriter = linkWriter;
    private readonly ILinksDbContext _linksDbContext = linksDbContext;

    public async Task<Result> Handle(UpdateLinkCommand request, CancellationToken cancellationToken)
    {
        var owner = await _accessResolver.ForManaging(request.OwnerType, request.OwnerId, cancellationToken);
        if (owner.IsFailure)
            return owner;

        var actor = await _accessResolver.ResolveActor(cancellationToken);

        var updated = await _linkWriter.Update(
            owner.Value.OwnerType, request.OwnerId, request.LinkId, request.Name, request.Url, actor, cancellationToken);
        if (updated.IsFailure)
            return updated;

        await _linksDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
