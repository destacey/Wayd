using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Application.Validation;
using Wayd.Common.Domain.Links;
using FluentValidation;

namespace Wayd.Links.Commands;

/// <summary>
/// Removes a link from a record. Only someone who may edit the record.
/// </summary>
/// <param name="OwnerType">The record's registered owner type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="LinkId">The link's id.</param>
public sealed record DeleteLinkCommand(string OwnerType, Guid OwnerId, Guid LinkId) : ICommand;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class DeleteLinkCommandValidator : CustomValidator<DeleteLinkCommand>
{
    public DeleteLinkCommandValidator()
    {
        RuleFor(c => c.OwnerType).NotEmpty().MaximumLength(LinkOwnerTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
        RuleFor(c => c.LinkId).NotEmpty();
    }
}

/// <summary>Removes the link after the record's owner type allows it, and saves.</summary>
public sealed class DeleteLinkCommandHandler(
    LinkOwnerAccessResolver accessResolver,
    ILinkWriter linkWriter,
    ILinksDbContext linksDbContext) : ICommandHandler<DeleteLinkCommand>
{
    private readonly LinkOwnerAccessResolver _accessResolver = accessResolver;
    private readonly ILinkWriter _linkWriter = linkWriter;
    private readonly ILinksDbContext _linksDbContext = linksDbContext;

    public async Task<Result> Handle(DeleteLinkCommand request, CancellationToken cancellationToken)
    {
        var owner = await _accessResolver.ForManaging(request.OwnerType, request.OwnerId, cancellationToken);
        if (owner.IsFailure)
            return owner;

        var actor = await _accessResolver.ResolveActor(cancellationToken);

        var removed = await _linkWriter.Remove(owner.Value.OwnerType, request.OwnerId, request.LinkId, actor, cancellationToken);
        if (removed.IsFailure)
            return removed;

        await _linksDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
