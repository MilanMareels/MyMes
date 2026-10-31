using MyMES.Contracts.Integrations;
using MyMES.Contracts.Machines;
using MyMES.Contracts.Production;
using MyMES.Contracts.Products;
using MyMES.Domain.Integrations;
using MyMES.Domain.Machines;
using MyMES.Domain.Production;
using MyMES.Domain.Products;

namespace MyMES.Application.Mapping;

/// <summary>
/// Hand-written projections from domain entities to wire contracts.
/// </summary>
/// <remarks>
/// Mapping is explicit rather than convention-based. A domain rename then breaks the build here,
/// where it is cheap to notice, instead of silently changing the public API shape at run time.
/// The enum maps are exhaustive and covered by tests, so adding a domain member cannot slip out
/// as a default value.
/// </remarks>
public static class DomainToContractMappings
{
    public static MachineStatusDto ToDto(this MachineStatus status) => status switch
    {
        MachineStatus.Unknown => MachineStatusDto.Unknown,
        MachineStatus.Idle => MachineStatusDto.Idle,
        MachineStatus.Running => MachineStatusDto.Running,
        MachineStatus.Faulted => MachineStatusDto.Faulted,
        MachineStatus.Maintenance => MachineStatusDto.Maintenance,
        MachineStatus.Decommissioned => MachineStatusDto.Decommissioned,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped machine status.")
    };

    public static MachineStatus ToDomain(this MachineStatusDto status) => status switch
    {
        MachineStatusDto.Unknown => MachineStatus.Unknown,
        MachineStatusDto.Idle => MachineStatus.Idle,
        MachineStatusDto.Running => MachineStatus.Running,
        MachineStatusDto.Faulted => MachineStatus.Faulted,
        MachineStatusDto.Maintenance => MachineStatus.Maintenance,
        MachineStatusDto.Decommissioned => MachineStatus.Decommissioned,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped machine status.")
    };

    public static MachineDto ToDto(this Machine machine) => new(
        machine.Id,
        machine.MachineCode,
        machine.Name,
        machine.Description,
        machine.Status.ToDto(),
        machine.StatusChangedAtUtc,
        machine.CreatedAtUtc,
        machine.UpdatedAtUtc);

    public static ProductionOrderStatusDto ToDto(this ProductionOrderStatus status) => status switch
    {
        ProductionOrderStatus.Draft => ProductionOrderStatusDto.Draft,
        ProductionOrderStatus.Released => ProductionOrderStatusDto.Released,
        ProductionOrderStatus.InProgress => ProductionOrderStatusDto.InProgress,
        ProductionOrderStatus.OnHold => ProductionOrderStatusDto.OnHold,
        ProductionOrderStatus.Completed => ProductionOrderStatusDto.Completed,
        ProductionOrderStatus.Cancelled => ProductionOrderStatusDto.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped order status.")
    };

    public static ProductionOrderStatus ToDomain(this ProductionOrderStatusDto status) => status switch
    {
        ProductionOrderStatusDto.Draft => ProductionOrderStatus.Draft,
        ProductionOrderStatusDto.Released => ProductionOrderStatus.Released,
        ProductionOrderStatusDto.InProgress => ProductionOrderStatus.InProgress,
        ProductionOrderStatusDto.OnHold => ProductionOrderStatus.OnHold,
        ProductionOrderStatusDto.Completed => ProductionOrderStatus.Completed,
        ProductionOrderStatusDto.Cancelled => ProductionOrderStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped order status.")
    };

    public static ProductionOrderDto ToDto(this ProductionOrder order, string? productVersionNumber = null) => new(
        order.Id,
        order.OrderNumber,
        order.ProductCode,
        order.ProductVersionId,
        productVersionNumber,
        order.PlannedQuantity,
        order.ProducedQuantity,
        order.CompletionPercentage,
        order.Status.ToDto(),
        order.PlannedStartUtc,
        order.PlannedEndUtc,
        order.ActualStartUtc,
        order.ActualEndUtc,
        order.CreatedAtUtc,
        order.UpdatedAtUtc);

    public static ProductVersionStatusDto ToDto(this ProductVersionStatus status) => status switch
    {
        ProductVersionStatus.Draft => ProductVersionStatusDto.Draft,
        ProductVersionStatus.Released => ProductVersionStatusDto.Released,
        ProductVersionStatus.Obsolete => ProductVersionStatusDto.Obsolete,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped revision status.")
    };

    public static ProductVersionStatus ToDomain(this ProductVersionStatusDto status) => status switch
    {
        ProductVersionStatusDto.Draft => ProductVersionStatus.Draft,
        ProductVersionStatusDto.Released => ProductVersionStatus.Released,
        ProductVersionStatusDto.Obsolete => ProductVersionStatus.Obsolete,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped revision status.")
    };

    public static ProductVersionDto ToDto(this ProductVersion version) => new(
        version.Id,
        version.ProductId,
        version.VersionNumber,
        version.Status.ToDto(),
        version.Notes,
        version.CreatedAtUtc,
        version.ReleasedAtUtc,
        version.ObsoletedAtUtc);

    public static ProductDto ToDto(this Product product)
    {
        var released = product.ReleasedVersion;
        return new ProductDto(
            product.Id,
            product.ProductCode,
            product.Name,
            product.Description,
            product.UnitOfMeasure,
            product.IsActive,
            released?.Id,
            released?.VersionNumber,
            [.. product.Versions.OrderBy(v => v.VersionNumber).Select(ToDto)],
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }

    public static ProductSummaryDto ToSummaryDto(this Product product) => new(
        product.Id,
        product.ProductCode,
        product.Name,
        product.UnitOfMeasure,
        product.IsActive,
        product.ReleasedVersion?.VersionNumber,
        product.Versions.Count);

    public static IncomingMessageStatusDto ToDto(this IncomingMessageStatus status) => status switch
    {
        IncomingMessageStatus.Processed => IncomingMessageStatusDto.Processed,
        IncomingMessageStatus.Failed => IncomingMessageStatusDto.Failed,
        _ => IncomingMessageStatusDto.New
    };

    public static IncomingErpMessageDto ToDto(this IncomingErpMessage message) => new(
        message.Id,
        message.ExternalReference,
        message.ProductCode,
        message.Status.ToDto(),
        message.Error,
        message.ReceivedAtUtc,
        message.ProcessedAtUtc);
}
