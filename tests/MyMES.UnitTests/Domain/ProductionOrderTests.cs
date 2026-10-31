using MyMES.Domain.Common;
using MyMES.Domain.Production;
using Shouldly;

namespace MyMES.UnitTests.Domain;

/// <summary>
/// The order state machine. These are the rules the rest of the system relies on, so they are
/// tested against the aggregate directly rather than through a service.
/// </summary>
public sealed class ProductionOrderTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);

    private static ProductionOrder NewOrder(decimal quantity = 100) =>
        ProductionOrder.Create("WO-0001", "FG-PUMP-100", quantity, Now, Now.AddDays(1));

    [Fact]
    public void Create_normalises_the_business_keys()
    {
        var order = ProductionOrder.Create(" wo-0001 ", " fg-pump-100 ", 10, Now, Now.AddHours(4));

        order.OrderNumber.ShouldBe("WO-0001");
        order.ProductCode.ShouldBe("FG-PUMP-100");
        order.Status.ShouldBe(ProductionOrderStatus.Draft);
        order.ProducedQuantity.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_non_positive_quantity(decimal quantity)
    {
        var act = () => ProductionOrder.Create("WO-0001", "FG-1", quantity, Now, Now.AddHours(1));

        var ex = Should.Throw<DomainException>(act);
        ex.Code.ShouldBe("production_order.quantity_invalid");
    }

    [Fact]
    public void Create_rejects_an_end_before_the_start()
    {
        var act = () => ProductionOrder.Create("WO-0001", "FG-1", 10, Now, Now.AddHours(-1));

        Should.Throw<DomainException>(act).Code.ShouldBe("production_order.dates_invalid");
    }

    [Fact]
    public void A_draft_order_cannot_be_started()
    {
        var order = NewOrder();

        Should.Throw<DomainException>(() => order.Start(Now))
            .Code.ShouldBe("production_order.not_startable");
    }

    [Fact]
    public void Release_then_start_records_the_actual_start()
    {
        var order = NewOrder();

        order.Release(Now);
        order.Start(Now.AddMinutes(5));

        order.Status.ShouldBe(ProductionOrderStatus.InProgress);
        order.ActualStartUtc.ShouldBe(Now.AddMinutes(5));
    }

    [Fact]
    public void Resuming_after_a_hold_keeps_the_original_start()
    {
        var order = NewOrder();
        order.Release(Now);
        order.Start(Now.AddMinutes(5));
        order.PutOnHold(Now.AddMinutes(30));

        order.Start(Now.AddMinutes(45));

        order.Status.ShouldBe(ProductionOrderStatus.InProgress);
        order.ActualStartUtc.ShouldBe(Now.AddMinutes(5));
    }

    [Fact]
    public void Production_can_only_be_booked_while_the_order_runs()
    {
        var order = NewOrder();
        order.Release(Now);

        Should.Throw<DomainException>(() => order.ReportProduction(5, Now))
            .Code.ShouldBe("production_order.not_running");
    }

    [Fact]
    public void Reported_quantities_accumulate_and_drive_the_percentage()
    {
        var order = NewOrder(quantity: 200);
        order.Release(Now);
        order.Start(Now);

        order.ReportProduction(50, Now.AddMinutes(10));
        order.ReportProduction(30, Now.AddMinutes(20));

        order.ProducedQuantity.ShouldBe(80);
        order.CompletionPercentage.ShouldBe(40);
    }

    [Fact]
    public void Over_production_is_accepted_and_the_percentage_caps_at_one_hundred()
    {
        // The floor really does make extra pieces. Refusing the booking would push operators
        // into not reporting at all, which is worse than a quantity above plan.
        var order = NewOrder(quantity: 100);
        order.Release(Now);
        order.Start(Now);

        order.ReportProduction(120, Now.AddHours(1));

        order.ProducedQuantity.ShouldBe(120);
        order.CompletionPercentage.ShouldBe(100);
    }

    [Fact]
    public void Completing_closes_the_order_and_stamps_the_end()
    {
        var order = NewOrder();
        order.Release(Now);
        order.Start(Now);
        order.ReportProduction(100, Now.AddHours(2));

        order.Complete(Now.AddHours(3));

        order.Status.ShouldBe(ProductionOrderStatus.Completed);
        order.ActualEndUtc.ShouldBe(Now.AddHours(3));
        order.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void A_closed_order_cannot_be_cancelled_again()
    {
        var order = NewOrder();
        order.Release(Now);
        order.Start(Now);
        order.Complete(Now.AddHours(1));

        Should.Throw<DomainException>(() => order.Cancel(Now.AddHours(2)))
            .Code.ShouldBe("production_order.closed");
    }

    [Fact]
    public void A_started_order_can_no_longer_be_rescheduled()
    {
        var order = NewOrder();
        order.Release(Now);
        order.Start(Now);

        Should.Throw<DomainException>(() => order.Reschedule(Now, Now.AddDays(2), Now))
            .Code.ShouldBe("production_order.started");
    }

    [Fact]
    public void The_product_revision_is_fixed_once_the_order_leaves_draft()
    {
        var order = NewOrder();
        order.Release(Now);

        Should.Throw<DomainException>(() => order.AssignProductVersion(Guid.CreateVersion7(), Now))
            .Code.ShouldBe("production_order.not_draft");
    }

    [Fact]
    public void The_lifecycle_raises_the_events_other_modules_react_to()
    {
        var order = NewOrder();
        order.Release(Now);
        order.Start(Now);
        order.ReportProduction(10, Now);
        order.Complete(Now.AddHours(1));

        order.DomainEvents.OfType<ProductionOrderReleasedEvent>().ShouldHaveSingleItem();
        order.DomainEvents.OfType<ProductionOrderStartedEvent>().ShouldHaveSingleItem();
        order.DomainEvents.OfType<ProductionReportedEvent>().ShouldHaveSingleItem();
        order.DomainEvents.OfType<ProductionOrderCompletedEvent>().ShouldHaveSingleItem();
    }
}
