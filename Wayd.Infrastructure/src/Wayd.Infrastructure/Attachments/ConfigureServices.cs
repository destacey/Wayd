using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Attachments;

namespace Wayd.Infrastructure.Attachments;

internal static class ConfigureServices
{
    /// <summary>
    /// Registers images in markdown: where their bytes are kept, how uploads are re-encoded, and who may read them.
    /// </summary>
    /// <remarks>
    /// Which records may hold images is decided by the read rules registered for following: an attachment's owner
    /// type must have one, since that rule is what decides who may read it.
    /// </remarks>
    internal static IServiceCollection AddAttachments(this IServiceCollection services)
    {
        services.AddScoped<IAttachmentStore, DatabaseAttachmentStore>();
        services.AddSingleton<IImageSanitizer, SkiaImageSanitizer>();
        services.AddScoped<AttachmentAccess>();
        services.AddScoped<IMarkdownAttachments, MarkdownAttachments>();

        return services;
    }
}
