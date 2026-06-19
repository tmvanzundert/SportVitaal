using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;

namespace SportVitaal.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="Membership"/> entity: validity window, extension and the
/// monthly-only cancellation rule.
/// </summary>
[TestFixture]
public class MembershipTests
{
    // ---- Construction (good & bad) ----

    [Test]
    public void Constructor_OpenEnded_IsActive()
    {
        var m = new Membership(MembershipType.UnlimitedMonthly, DateTime.UtcNow, null);
        Assert.That(m.IsActive, Is.True);
    }

    [Test]
    public void Constructor_WithTypeNone_Throws()
        => Assert.Throws<DomainException>(() => new Membership(MembershipType.None, DateTime.UtcNow, null));

    [Test]
    public void Constructor_EndBeforeStart_Throws()
    {
        var start = DateTime.UtcNow;
        Assert.Throws<DomainException>(() => new Membership(MembershipType.UnlimitedMonthly, start, start.AddDays(-1)));
    }

    // ---- IsActive ----

    [Test]
    public void IsActive_WhenEndDateInFuture_IsTrue()
    {
        var m = new Membership(MembershipType.UnlimitedYearly, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(10));
        Assert.That(m.IsActive, Is.True);
    }

    [Test]
    public void IsActive_WhenEndDateInPast_IsFalse()
    {
        var m = new Membership(MembershipType.UnlimitedYearly, DateTime.UtcNow.AddYears(-2), DateTime.UtcNow.AddDays(-1));
        Assert.That(m.IsActive, Is.False);
    }

    // ---- Extend ----

    [Test]
    public void Extend_ToLaterDate_UpdatesEndDate()
    {
        var m = new Membership(MembershipType.UnlimitedMonthly, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        var newEnd = DateTime.UtcNow.AddMonths(2);

        m.Extend(newEnd);

        Assert.That(m.EndDate, Is.EqualTo(newEnd));
    }

    [Test]
    public void Extend_ToEarlierDate_KeepsExistingEndDate()
    {
        var end = DateTime.UtcNow.AddMonths(3);
        var m = new Membership(MembershipType.UnlimitedMonthly, DateTime.UtcNow, end);

        m.Extend(DateTime.UtcNow.AddMonths(1)); // earlier than current end -> ignored

        Assert.That(m.EndDate, Is.EqualTo(end));
    }

    [Test]
    public void Extend_BeforeStartDate_Throws()
    {
        var start = DateTime.UtcNow;
        var m = new Membership(MembershipType.UnlimitedMonthly, start, start.AddMonths(1));
        Assert.Throws<DomainException>(() => m.Extend(start.AddDays(-1)));
    }

    // ---- CancelIfMonthly (good & bad) ----

    [TestCase(MembershipType.TwiceWeeklyMonthly)]
    [TestCase(MembershipType.UnlimitedMonthly)]
    public void CancelIfMonthly_OnMonthlyPlan_StampsEndDateWhenOpenEnded(MembershipType type)
    {
        var m = new Membership(type, DateTime.UtcNow.AddDays(-5), null);

        m.CancelIfMonthly();

        Assert.That(m.EndDate, Is.Not.Null);
    }

    [Test]
    public void CancelIfMonthly_OnMonthlyPlan_KeepsExistingFutureEndDate()
    {
        var end = DateTime.UtcNow.AddDays(10);
        var m = new Membership(MembershipType.TwiceWeeklyMonthly, DateTime.UtcNow.AddDays(-5), end);

        m.CancelIfMonthly();

        Assert.That(m.EndDate, Is.EqualTo(end));
    }

    [TestCase(MembershipType.TwiceWeeklyYearly)]
    [TestCase(MembershipType.UnlimitedYearly)]
    public void CancelIfMonthly_OnYearlyPlan_Throws(MembershipType type)
    {
        var m = new Membership(type, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        Assert.Throws<DomainException>(() => m.CancelIfMonthly());
    }
}
