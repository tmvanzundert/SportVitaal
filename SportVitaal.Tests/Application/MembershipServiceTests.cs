using SportVitaal.Application.Services;
using SportVitaal.Domain.DomainEvents;
using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;
using SportVitaal.Domain.ValueObjects;
using SportVitaal.Tests.Fakes;

namespace SportVitaal.Tests.Application;

/// <summary>
/// Unit tests for <see cref="MembershipService"/>: purchase (end-date per billing period), upgrade,
/// renew (extend) and cancel.
/// </summary>
[TestFixture]
public class MembershipServiceTests
{
    private InMemoryStore _store = null!;
    private RecordingDispatcher _dispatcher = null!;
    private MembershipService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new InMemoryStore();
        _dispatcher = new RecordingDispatcher();
        _service = new MembershipService(
            new FakeUserRepository(_store),
            new FakeMembershipRepository(_store),
            new FakeUnitOfWork(_store),
            _dispatcher);
    }

    private UserAccount AddUser()
    {
        var user = new UserAccount($"{Guid.NewGuid():N}@test.com", Role.Member);
        _store.Users.Add(user);
        return user;
    }

    // ---- PurchaseMembershipAsync ----

    [Test]
    public async Task PurchaseMembershipAsync_Monthly_SetsEndDateOneMonthLater()
    {
        var user = AddUser();
        var start = new DateTime(2026, 6, 1);

        await _service.PurchaseMembershipAsync(user.Id, MembershipType.UnlimitedMonthly, start, new Money(55m));

        Assert.That(user.Membership!.EndDate, Is.EqualTo(start.AddMonths(1)));
        Assert.That(_dispatcher.Dispatched.OfType<MembershipPurchasedEvent>().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task PurchaseMembershipAsync_Yearly_SetsEndDateOneYearLater()
    {
        var user = AddUser();
        var start = new DateTime(2026, 6, 1);

        await _service.PurchaseMembershipAsync(user.Id, MembershipType.TwiceWeeklyYearly, start, new Money(299m));

        Assert.That(user.Membership!.EndDate, Is.EqualTo(start.AddYears(1)));
    }

    [Test]
    public void PurchaseMembershipAsync_UnknownUser_Throws()
        => Assert.ThrowsAsync<DomainException>(() =>
            _service.PurchaseMembershipAsync(Guid.NewGuid(), MembershipType.UnlimitedMonthly, DateTime.UtcNow, new Money(55m)));

    // ---- UpgradeMembershipAsync ----

    [Test]
    public async Task UpgradeMembershipAsync_KeepsPeriodAndChangesTier()
    {
        var user = AddUser();
        var start = DateTime.UtcNow.Date;
        await _service.PurchaseMembershipAsync(user.Id, MembershipType.TwiceWeeklyMonthly, start, new Money(29m));
        var originalEnd = user.Membership!.EndDate;

        await _service.UpgradeMembershipAsync(user.Id, MembershipType.UnlimitedMonthly, new Money(26m));

        Assert.Multiple(() =>
        {
            Assert.That(user.Membership!.Type, Is.EqualTo(MembershipType.UnlimitedMonthly));
            Assert.That(user.Membership.EndDate, Is.EqualTo(originalEnd)); // same paid-for period
        });
    }

    [Test]
    public void UpgradeMembershipAsync_WithoutMembership_Throws()
    {
        var user = AddUser();
        Assert.ThrowsAsync<DomainException>(() =>
            _service.UpgradeMembershipAsync(user.Id, MembershipType.UnlimitedMonthly, new Money(26m)));
    }

    // ---- RenewMembershipAsync ----

    [Test]
    public async Task RenewMembershipAsync_Yearly_ExtendsByOneYearFromCurrentEnd()
    {
        var user = AddUser();
        var start = new DateTime(2026, 1, 1);
        await _service.PurchaseMembershipAsync(user.Id, MembershipType.UnlimitedYearly, start, new Money(549m));
        var endBefore = user.Membership!.EndDate!.Value;

        await _service.RenewMembershipAsync(user.Id);

        Assert.That(user.Membership!.EndDate, Is.EqualTo(endBefore.AddYears(1)));
    }

    [Test]
    public void RenewMembershipAsync_WithoutMembership_Throws()
    {
        var user = AddUser();
        Assert.ThrowsAsync<DomainException>(() => _service.RenewMembershipAsync(user.Id));
    }

    // ---- CancelMembershipAsync ----

    [Test]
    public async Task CancelMembershipAsync_Monthly_StampsEndDateAndDispatchesEvent()
    {
        var user = AddUser();
        await _service.PurchaseMembershipAsync(user.Id, MembershipType.UnlimitedMonthly, DateTime.UtcNow.Date, new Money(55m));

        await _service.CancelMembershipAsync(user.Id, user.Id);

        Assert.That(user.Membership!.EndDate, Is.Not.Null);
        Assert.That(_dispatcher.Dispatched.OfType<MembershipExpiringSoonEvent>().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task CancelMembershipAsync_Yearly_Throws()
    {
        var user = AddUser();
        await _service.PurchaseMembershipAsync(user.Id, MembershipType.UnlimitedYearly, DateTime.UtcNow.Date, new Money(549m));

        Assert.ThrowsAsync<DomainException>(() => _service.CancelMembershipAsync(user.Id, user.Id));
    }

    [Test]
    public void CancelMembershipAsync_WithoutMembership_Throws()
    {
        var user = AddUser();
        Assert.ThrowsAsync<DomainException>(() => _service.CancelMembershipAsync(user.Id, user.Id));
    }
}
