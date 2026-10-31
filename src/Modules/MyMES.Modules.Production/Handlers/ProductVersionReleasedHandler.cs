using Microsoft.Extensions.Logging;
using MyMES.Domain.Products;
using MyMES.EventBus;

namespace MyMES.Modules.Production.Handlers;

/// <summary>
/// Records that the plant's build standard for a product has moved to a new revision.
/// </summary>
/// <remarks>
/// Today it writes an audit line. It is the seam where the real behaviour goes: warning about
/// open orders still tied to the superseded revision, and notifying planning that work in
/// progress may need re-releasing.
/// </remarks>
internal sealed class ProductVersionReleasedHandler(ILogger<ProductVersionReleasedHandler> logger)
    : IEventHandler<ProductVersionReleasedEvent>
{
    public Task HandleAsync(ProductVersionReleasedEvent message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.SupersededVersionNumber is { } superseded)
        {
            logger.LogInformation(
                "Build standard for {ProductCode} moved from revision {Previous} to {Current}.",
                message.ProductCode,
                superseded,
                message.VersionNumber);
        }
        else
        {
            logger.LogInformation(
                "Product {ProductCode} has its first released revision {Current}.",
                message.ProductCode,
                message.VersionNumber);
        }

        return Task.CompletedTask;
    }
}
