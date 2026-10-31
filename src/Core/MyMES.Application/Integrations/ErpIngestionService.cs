using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Results;
using MyMES.Abstractions.Time;
using MyMES.Application.Abstractions;
using MyMES.Application.Mapping;
using MyMES.Application.Products;
using MyMES.Contracts.Integrations;
using MyMES.Contracts.Products;
using MyMES.Domain.Integrations;

namespace MyMES.Application.Integrations;

/// <inheritdoc cref="IErpIngestionService"/>
public sealed class ErpIngestionService(IApplicationDbContext db,IProductService products,IDateTimeProvider clock, ILogger<ErpIngestionService> logger) : IErpIngestionService
{
    public async Task<Result<IncomingErpMessageDto>> ReceiveAsync(ReceiveErpProductRequest request, CancellationToken ct = default)
    {
        var exists = await db.IncomingErpMessages
            .AnyAsync(m => m.ExternalReference == request.ExternalReference, ct)
            .ConfigureAwait(false);

        if (exists)
        {
            return Result.Failure<IncomingErpMessageDto>(Error.Conflict("erp.duplicate_reference", $"A message with reference {request.ExternalReference} already exists."));
        }

        var message = IncomingErpMessage.Create(
            request.ExternalReference,
            request.ProductCode,
            request.Payload,
            clock.UtcNow);

        db.IncomingErpMessages.Add(message);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("ERP message {Reference} received.", message.ExternalReference);
        return Result.Success(message.ToDto());
    }

    public async Task<IReadOnlyList<IncomingErpMessageDto>> GetAsync(
        IncomingMessageStatusDto? status = null,
        CancellationToken ct = default)
    {
        var query = db.IncomingErpMessages.AsNoTracking();

        if (status is { } wanted)
        {
            var domainStatus = ToDomain(wanted);
            query = query.Where(m => m.Status == domainStatus);
        }

        var items = await query
            .OrderByDescending(m => m.ReceivedAtUtc)
            .Take(500)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. items.Select(m => m.ToDto())];
    }

    public async Task<int> ProcessPendingAsync(CancellationToken ct = default)
    {
        var pending = await db.IncomingErpMessages
            .Where(m => m.Status == IncomingMessageStatus.New)
            .OrderBy(m => m.ReceivedAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var message in pending)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var payload = ParsePayload(message.Payload);

                var result = await products.CreateAsync(
                    new CreateProductRequest
                    {
                        ProductCode = message.ProductCode,
                        Name = payload.Name,
                        Description = payload.Description,
                        UnitOfMeasure = payload.UnitOfMeasure ?? "pcs"
                    },
                    ct).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    message.MarkProcessed(clock.UtcNow);
                }
                else
                {
                    var error = result.Error!.Value;
                    message.MarkFailed($"{error.Code}: {error.Message}", clock.UtcNow);
                }
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message, clock.UtcNow);
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("ERP: {Count} message(s) processed.", pending.Count);

        return pending.Count;
    }

    private static IncomingMessageStatus ToDomain(IncomingMessageStatusDto status) => status switch
    {
        IncomingMessageStatusDto.Processed => IncomingMessageStatus.Processed,
        IncomingMessageStatusDto.Failed => IncomingMessageStatus.Failed,
        _ => IncomingMessageStatus.New
    };

    /// <summary>
    /// De payload is een JSON-object met ten minste een <c>name</c>, en optioneel
    /// <c>description</c> en <c>unitOfMeasure</c>.
    /// </summary>
    private static (string Name, string? Description, string? UnitOfMeasure) ParsePayload(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? string.Empty
            : string.Empty;

        var description = root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
            ? d.GetString()
            : null;

        var unitOfMeasure = root.TryGetProperty("unitOfMeasure", out var u) && u.ValueKind == JsonValueKind.String
            ? u.GetString()
            : null;

        return (name, description, unitOfMeasure);
    }
}
