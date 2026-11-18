using System.Security.Cryptography;
using Wayd.Common.Application.Attachments.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Attachments;

namespace Wayd.Common.Application.Attachments.Commands;

/// <summary>
/// Stores an uploaded image as a pending attachment that only the uploader can read, until a save of the record
/// whose markdown refers to it claims it.
/// </summary>
/// <param name="Content">The uploaded file. Read once; the caller disposes it.</param>
/// <param name="Length">The file's length in bytes, as the request declared it.</param>
public sealed record UploadAttachmentCommand(Stream Content, long Length) : ICommand<AttachmentDto>;

/// <summary>Refuses an empty file and one over <see cref="AttachmentLimits.MaxUploadBytes"/>.</summary>
public sealed class UploadAttachmentCommandValidator : CustomValidator<UploadAttachmentCommand>
{
    public UploadAttachmentCommandValidator()
    {
        RuleFor(c => c.Content).NotNull();
        RuleFor(c => c.Length)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(AttachmentLimits.MaxUploadBytes).WithMessage("Images can be at most 5 MB.");
    }
}

/// <summary>Re-encodes the image, then stores it and its pending attachment together.</summary>
public sealed class UploadAttachmentCommandHandler(
    IAttachmentsDbContext dbContext,
    IAttachmentStore store,
    IImageSanitizer imageSanitizer,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<UploadAttachmentCommandHandler> logger)
    : ICommandHandler<UploadAttachmentCommand, AttachmentDto>
{
    private readonly IAttachmentsDbContext _dbContext = dbContext;
    private readonly IAttachmentStore _store = store;
    private readonly IImageSanitizer _imageSanitizer = imageSanitizer;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UploadAttachmentCommandHandler> _logger = logger;

    public async Task<Result<AttachmentDto>> Handle(UploadAttachmentCommand request, CancellationToken cancellationToken)
    {
        var image = await _imageSanitizer.Sanitize(request.Content, cancellationToken);
        if (image.IsFailure)
        {
            _logger.LogInformation("Refused an image upload: {Error}", image.Error);
            return Result.Failure<AttachmentDto>(image.Error);
        }

        var data = image.Value.Data;
        var attachment = Attachment.CreatePending(
            image.Value.ContentType,
            data.LongLength,
            image.Value.Width,
            image.Value.Height,
            Convert.ToHexStringLower(SHA256.HashData(data)),
            _currentUser.GetUserId(),
            _dateTimeProvider.Now);

        _dbContext.Attachments.Add(attachment);
        _store.Write(attachment.Id, data);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment {AttachmentId} uploaded ({ContentType}, {Size} bytes).", attachment.Id, attachment.ContentType, attachment.Size);

        return AttachmentDto.From(attachment);
    }
}
