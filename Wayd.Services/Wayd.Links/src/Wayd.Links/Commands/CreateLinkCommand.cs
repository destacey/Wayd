using CSharpFunctionalExtensions;
using FluentValidation;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Application.Validation;
using Wayd.Common.Domain.Links;

namespace Wayd.Links.Commands;

/// <summary>
/// Adds a link to a record. Only someone who may edit the record.
/// </summary>
/// <param name="OwnerType">The record's registered owner type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="Name">What the link is called.</param>
/// <param name="Url">The web address the link opens.</param>
public sealed record CreateLinkCommand(string OwnerType, Guid OwnerId, string Name, string Url) : ICommand<Guid>;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class CreateLinkCommandValidator : CustomValidator<CreateLinkCommand>
{
    public CreateLinkCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.OwnerType).NotEmpty().MaximumLength(LinkOwnerTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Link.NameMaxLength);
        RuleFor(c => c.Url).NotEmpty().Must(Link.IsWebAddress).WithMessage(LinkErrors.UrlNotWebAddress);
    }
}

/// <summary>Adds the link after the record's owner type allows it, and saves.</summary>
public sealed class CreateLinkCommandHandler(
    LinkOwnerAccessResolver accessResolver,
    ILinkWriter linkWriter,
    ILinksDbContext linksDbContext) : ICommandHandler<CreateLinkCommand, Guid>
{
    private readonly LinkOwnerAccessResolver _accessResolver = accessResolver;
    private readonly ILinkWriter _linkWriter = linkWriter;
    private readonly ILinksDbContext _linksDbContext = linksDbContext;

    public async Task<Result<Guid>> Handle(CreateLinkCommand request, CancellationToken cancellationToken)
    {
        var owner = await _accessResolver.ForManaging(request.OwnerType, request.OwnerId, cancellationToken);
        if (owner.IsFailure)
            return Result.Failure<Guid>(owner.Error);

        var actor = await _accessResolver.ResolveActor(cancellationToken);

        var added = await _linkWriter.Add(owner.Value.OwnerType, request.OwnerId, request.Name, request.Url, actor, cancellationToken);
        if (added.IsFailure)
            return added;

        await _linksDbContext.SaveChangesAsync(cancellationToken);

        return added;
    }
}
