using MyMES.Domain.Common;
using MyMES.Domain.Products;
using Shouldly;

namespace MyMES.UnitTests.Domain;

/// <summary>
/// Product revisions. The invariant worth protecting is "at most one released revision", which
/// is why the aggregate root owns the versions rather than exposing them as their own entity.
/// </summary>
public sealed class ProductTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);

    private static Product NewProduct() =>
        Product.Create("fg-pump-100", "Hydraulic pump HP-100", null, "pcs");

    [Fact]
    public void Create_normalises_the_code_and_starts_active()
    {
        var product = NewProduct();

        product.ProductCode.ShouldBe("FG-PUMP-100");
        product.IsActive.ShouldBeTrue();
        product.Versions.ShouldBeEmpty();
        product.ReleasedVersion.ShouldBeNull();
    }

    [Fact]
    public void A_new_revision_starts_as_a_draft()
    {
        var product = NewProduct();

        var version = product.AddVersion("a", "first cut", Now);

        version.VersionNumber.ShouldBe("A");
        version.Status.ShouldBe(ProductVersionStatus.Draft);
        product.Versions.Count.ShouldBe(1);
    }

    [Fact]
    public void A_revision_number_is_unique_within_the_product()
    {
        var product = NewProduct();
        product.AddVersion("A", null, Now);

        Should.Throw<DomainException>(() => product.AddVersion("a", null, Now))
            .Code.ShouldBe("product_version.duplicate");
    }

    [Fact]
    public void Releasing_a_revision_retires_the_one_that_was_released_before()
    {
        var product = NewProduct();
        var a = product.AddVersion("A", null, Now);
        var b = product.AddVersion("B", null, Now);

        product.ReleaseVersion(a.Id, Now);
        product.ReleaseVersion(b.Id, Now.AddDays(1));

        a.Status.ShouldBe(ProductVersionStatus.Obsolete);
        a.ObsoletedAtUtc.ShouldBe(Now.AddDays(1));
        b.Status.ShouldBe(ProductVersionStatus.Released);
        product.ReleasedVersion.ShouldBe(b);
    }

    [Fact]
    public void A_product_never_has_two_released_revisions()
    {
        var product = NewProduct();
        var a = product.AddVersion("A", null, Now);
        var b = product.AddVersion("B", null, Now);
        var c = product.AddVersion("C", null, Now);

        product.ReleaseVersion(a.Id, Now);
        product.ReleaseVersion(b.Id, Now.AddHours(1));
        product.ReleaseVersion(c.Id, Now.AddHours(2));

        product.Versions.Count(v => v.Status == ProductVersionStatus.Released).ShouldBe(1);
    }

    [Fact]
    public void Only_a_draft_can_be_released()
    {
        var product = NewProduct();
        var a = product.AddVersion("A", null, Now);
        product.ReleaseVersion(a.Id, Now);

        Should.Throw<DomainException>(() => product.ReleaseVersion(a.Id, Now.AddHours(1)))
            .Code.ShouldBe("product_version.not_draft");
    }

    [Fact]
    public void Releasing_an_unknown_revision_is_rejected()
    {
        var product = NewProduct();

        Should.Throw<DomainException>(() => product.ReleaseVersion(Guid.CreateVersion7(), Now))
            .Code.ShouldBe("product_version.not_found");
    }

    [Fact]
    public void Deactivating_a_product_retires_its_released_revision()
    {
        var product = NewProduct();
        var a = product.AddVersion("A", null, Now);
        product.ReleaseVersion(a.Id, Now);

        product.Deactivate(Now.AddDays(5));

        product.IsActive.ShouldBeFalse();
        a.Status.ShouldBe(ProductVersionStatus.Obsolete);
        product.ReleasedVersion.ShouldBeNull();
    }

    [Fact]
    public void An_inactive_product_accepts_no_new_revisions()
    {
        var product = NewProduct();
        product.Deactivate(Now);

        Should.Throw<DomainException>(() => product.AddVersion("A", null, Now))
            .Code.ShouldBe("product.inactive");
    }

    [Fact]
    public void A_released_revision_is_frozen()
    {
        var product = NewProduct();
        var a = product.AddVersion("A", "draft notes", Now);
        product.ReleaseVersion(a.Id, Now);

        Should.Throw<DomainException>(() => a.UpdateNotes("changed after release"))
            .Code.ShouldBe("product_version.frozen");
    }

    [Fact]
    public void Releasing_raises_an_event_naming_the_revision_it_superseded()
    {
        var product = NewProduct();
        var a = product.AddVersion("A", null, Now);
        var b = product.AddVersion("B", null, Now);

        product.ReleaseVersion(a.Id, Now);
        product.ReleaseVersion(b.Id, Now.AddDays(1));

        var released = product.DomainEvents.OfType<ProductVersionReleasedEvent>().ToList();
        released.Count.ShouldBe(2);
        released[0].SupersededVersionNumber.ShouldBeNull();
        released[1].SupersededVersionNumber.ShouldBe("A");
    }
}
