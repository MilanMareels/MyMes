using MyMES.Domain.Common;
using MyMES.Domain.Machines;
using Shouldly;

namespace MyMES.UnitTests.Domain;

public sealed class MachineTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_normalises_the_code_and_starts_idle()
    {
        var machine = Machine.Create(" cnc-01 ", " CNC Lathe 1 ", null, Now);

        machine.MachineCode.ShouldBe("CNC-01");
        machine.Name.ShouldBe("CNC Lathe 1");
        machine.Status.ShouldBe(MachineStatus.Idle);
        machine.StatusChangedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Create_requires_a_code()
    {
        Should.Throw<DomainException>(() => Machine.Create("  ", "Name", null, Now))
            .Code.ShouldBe("machine.code_required");
    }

    [Fact]
    public void Changing_status_moves_the_timestamp_and_raises_an_event()
    {
        var machine = Machine.Create("CNC-01", "Lathe", null, Now);
        machine.ClearDomainEvents();

        machine.ChangeStatus(MachineStatus.Running, Now.AddMinutes(10));

        machine.Status.ShouldBe(MachineStatus.Running);
        machine.StatusChangedAtUtc.ShouldBe(Now.AddMinutes(10));

        var raised = machine.DomainEvents.OfType<MachineStatusChangedEvent>().ShouldHaveSingleItem();
        raised.PreviousStatus.ShouldBe(MachineStatus.Idle);
        raised.NewStatus.ShouldBe(MachineStatus.Running);
    }

    [Fact]
    public void Setting_the_same_status_changes_nothing()
    {
        // Machines are polled continuously; re-reporting the current state must not fill the
        // audit trail with duplicates.
        var machine = Machine.Create("CNC-01", "Lathe", null, Now);
        machine.ClearDomainEvents();

        machine.ChangeStatus(MachineStatus.Idle, Now.AddMinutes(10));

        machine.StatusChangedAtUtc.ShouldBe(Now);
        machine.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void A_decommissioned_machine_never_returns_to_service()
    {
        var machine = Machine.Create("CNC-01", "Lathe", null, Now);
        machine.Decommission(Now.AddDays(1));

        Should.Throw<DomainException>(() => machine.ChangeStatus(MachineStatus.Running, Now.AddDays(2)))
            .Code.ShouldBe("machine.decommissioned");
    }
}
