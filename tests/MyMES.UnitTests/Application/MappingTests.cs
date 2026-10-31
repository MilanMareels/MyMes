using MyMES.Application.Mapping;
using MyMES.Contracts.Machines;
using MyMES.Contracts.Production;
using MyMES.Contracts.Products;
using MyMES.Domain.Machines;
using MyMES.Domain.Production;
using MyMES.Domain.Products;
using Shouldly;

namespace MyMES.UnitTests.Application;

/// <summary>
/// Guards the seam between the domain and the public contract.
/// </summary>
/// <remarks>
/// The wire enums are separate types from the domain ones so the API can version independently.
/// The price of that freedom is that the two can silently drift apart, so every member is checked
/// in both directions: adding a domain value without a contract value fails here, loudly, instead
/// of serialising as something wrong at run time.
/// </remarks>
public sealed class MappingTests
{
    [Fact]
    public void Every_machine_status_maps_to_the_contract_and_back()
    {
        foreach (var status in Enum.GetValues<MachineStatus>())
        {
            status.ToDto().ToDomain().ShouldBe(status);
        }
    }

    [Fact]
    public void Every_machine_status_on_the_wire_maps_to_the_domain_and_back()
    {
        foreach (var status in Enum.GetValues<MachineStatusDto>())
        {
            status.ToDomain().ToDto().ShouldBe(status);
        }
    }

    [Fact]
    public void Every_order_status_maps_to_the_contract_and_back()
    {
        foreach (var status in Enum.GetValues<ProductionOrderStatus>())
        {
            status.ToDto().ToDomain().ShouldBe(status);
        }
    }

    [Fact]
    public void Every_order_status_on_the_wire_maps_to_the_domain_and_back()
    {
        foreach (var status in Enum.GetValues<ProductionOrderStatusDto>())
        {
            status.ToDomain().ToDto().ShouldBe(status);
        }
    }

    [Fact]
    public void Every_revision_status_maps_to_the_contract_and_back()
    {
        foreach (var status in Enum.GetValues<ProductVersionStatus>())
        {
            status.ToDto().ToDomain().ShouldBe(status);
        }
    }

    [Fact]
    public void The_two_enum_families_have_the_same_number_of_members()
    {
        // A new domain member without a contract member would otherwise only surface the first
        // time something of that value is serialised.
        Enum.GetValues<MachineStatus>().Length.ShouldBe(Enum.GetValues<MachineStatusDto>().Length);
        Enum.GetValues<ProductionOrderStatus>().Length.ShouldBe(Enum.GetValues<ProductionOrderStatusDto>().Length);
        Enum.GetValues<ProductVersionStatus>().Length.ShouldBe(Enum.GetValues<ProductVersionStatusDto>().Length);
    }

    [Fact]
    public void A_product_projects_its_released_revision_onto_the_contract()
    {
        var now = new DateTime(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);
        var product = Product.Create("FG-1", "Pump", "A pump", "pcs");
        var a = product.AddVersion("A", null, now);
        product.AddVersion("B", null, now);
        product.ReleaseVersion(a.Id, now);

        var dto = product.ToDto();

        dto.ReleasedVersionNumber.ShouldBe("A");
        dto.ReleasedVersionId.ShouldBe(a.Id);
        dto.Versions.Count.ShouldBe(2);
    }

    [Fact]
    public void An_order_carries_its_revision_label_when_one_is_supplied()
    {
        var now = new DateTime(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);
        var order = ProductionOrder.Create("WO-1", "FG-1", 100, now, now.AddDays(1));

        order.ToDto("B").ProductVersionNumber.ShouldBe("B");
        order.ToDto().ProductVersionNumber.ShouldBeNull();
    }
}
