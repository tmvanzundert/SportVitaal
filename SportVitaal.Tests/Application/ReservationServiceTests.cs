using SportVitaal.Application.Services;
using SportVitaal.Domain.DomainEvents;
using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;
using SportVitaal.Tests.Fakes;

namespace SportVitaal.Tests.Application;

/// <summary>
/// Unit tests for <see cref="ReservationService"/> covering the reservation window, membership
/// requirements, the weekly limit for "2x per week" plans, waiting-list handling, the cancellation
/// window and check-in window.
/// </summary>
[TestFixture]
public class ReservationServiceTests
{
    private InMemoryStore _store = null!;
    private RecordingDispatcher _dispatcher = null!;
    private ReservationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new InMemoryStore();
        _dispatcher = new RecordingDispatcher();
        _service = new ReservationService(
            new FakeLessonRepository(_store),
            new FakeReservationRepository(_store),
            new FakeWaitingListRepository(_store),
            new FakeUserRepository(_store),
            new FakeUnitOfWork(_store),
            _dispatcher);
    }

    private UserAccount AddMember(MembershipType type = MembershipType.UnlimitedMonthly, bool active = true)
    {
        var user = new UserAccount($"{Guid.NewGuid():N}@test.com", Role.Member);
        DateTime? end = active ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddDays(-1);
        user.StartMembership(new Membership(type, DateTime.UtcNow.AddMonths(-1), end));
        _store.Users.Add(user);
        return user;
    }

    private Lesson AddLesson(Location location, DateTime? startAt = null)
    {
        var lesson = new Lesson(Guid.NewGuid(), startAt ?? DateTime.UtcNow.AddDays(1), 60, location);
        _store.Lessons.Add(lesson);
        return lesson;
    }

    private static Location GroupRoom(int capacity = 3) => new("Zaal", capacity);
    private static Location Spinning(int capacity = 24) => new("Spinning", capacity, allowsSeatSelection: true);

    // ---- ReserveAsync: happy path ----

    [Test]
    public async Task ReserveAsync_ValidMemberAndLesson_CreatesReservationAndDispatchesEvent()
    {
        var member = AddMember();
        var lesson = AddLesson(GroupRoom());

        await _service.ReserveAsync(member.Id, lesson.Id);

        Assert.That(_store.Reservations, Has.Count.EqualTo(1));
        Assert.That(_dispatcher.Dispatched.OfType<ReservationCreatedEvent>().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task ReserveAsync_SpinningWithSeat_StoresSeat()
    {
        var member = AddMember();
        var lesson = AddLesson(Spinning());

        await _service.ReserveAsync(member.Id, lesson.Id, seatNumber: 12);

        Assert.That(_store.Reservations.Single().SeatNumber, Is.EqualTo(12));
    }

    // ---- ReserveAsync: bad flows ----

    [Test]
    public void ReserveAsync_UnknownLesson_Throws()
    {
        var member = AddMember();
        Assert.ThrowsAsync<DomainException>(() => _service.ReserveAsync(member.Id, Guid.NewGuid()));
    }

    [Test]
    public void ReserveAsync_MoreThanOneWeekAhead_Throws()
    {
        var member = AddMember();
        var lesson = AddLesson(GroupRoom(), DateTime.UtcNow.AddDays(8));
        Assert.ThrowsAsync<DomainException>(() => _service.ReserveAsync(member.Id, lesson.Id));
    }

    [Test]
    public void ReserveAsync_WithoutActiveMembership_Throws()
    {
        var member = AddMember(active: false);
        var lesson = AddLesson(GroupRoom());
        Assert.ThrowsAsync<DomainException>(() => _service.ReserveAsync(member.Id, lesson.Id));
    }

    [Test]
    public async Task ReserveAsync_InstructorWithoutMembership_Reserves()
    {
        // Instructors take part in lessons as staff and may always reserve, even with no membership.
        var instructor = new UserAccount($"{Guid.NewGuid():N}@test.com", Role.Instructor);
        _store.Users.Add(instructor);
        var lesson = AddLesson(GroupRoom());

        await _service.ReserveAsync(instructor.Id, lesson.Id);

        Assert.That(_store.Reservations, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReserveAsync_Instructor_NotSubjectToWeeklyCap()
    {
        // The membership-based weekly cap does not apply to instructors, so more than two in a week is fine.
        var instructor = new UserAccount($"{Guid.NewGuid():N}@test.com", Role.Instructor);
        _store.Users.Add(instructor);
        var monday = DateTime.UtcNow.AddDays(1);
        var l1 = AddLesson(GroupRoom(), monday);
        var l2 = AddLesson(GroupRoom(), monday.AddHours(2));
        var l3 = AddLesson(GroupRoom(), monday.AddHours(4));

        await _service.ReserveAsync(instructor.Id, l1.Id);
        await _service.ReserveAsync(instructor.Id, l2.Id);
        await _service.ReserveAsync(instructor.Id, l3.Id);

        Assert.That(_store.Reservations, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ReserveAsync_TwiceWeekly_ThirdReservationInWeek_Throws()
    {
        var member = AddMember(MembershipType.TwiceWeeklyMonthly);
        // Three lessons in the same Mon-Sun week, all within the 1-week reservation window.
        var monday = ThisMonday();
        var l1 = AddLesson(GroupRoom(), monday.AddHours(9));
        var l2 = AddLesson(GroupRoom(), monday.AddDays(1).AddHours(9));
        var l3 = AddLesson(GroupRoom(), monday.AddDays(2).AddHours(9));

        await _service.ReserveAsync(member.Id, l1.Id);
        await _service.ReserveAsync(member.Id, l2.Id);

        Assert.ThrowsAsync<DomainException>(() => _service.ReserveAsync(member.Id, l3.Id));
    }

    [Test]
    public async Task ReserveAsync_Unlimited_AllowsManyInWeek()
    {
        var member = AddMember(MembershipType.UnlimitedMonthly);
        var monday = ThisMonday();
        var l1 = AddLesson(GroupRoom(), monday.AddHours(9));
        var l2 = AddLesson(GroupRoom(), monday.AddDays(1).AddHours(9));
        var l3 = AddLesson(GroupRoom(), monday.AddDays(2).AddHours(9));

        await _service.ReserveAsync(member.Id, l1.Id);
        await _service.ReserveAsync(member.Id, l2.Id);
        await _service.ReserveAsync(member.Id, l3.Id);

        Assert.That(_store.Reservations, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ReserveAsync_WhenFull_JoinsWaitingList()
    {
        var taker = AddMember();
        var waiter = AddMember();
        var lesson = AddLesson(GroupRoom(capacity: 1));

        await _service.ReserveAsync(taker.Id, lesson.Id);
        await _service.ReserveAsync(waiter.Id, lesson.Id);

        Assert.That(_store.WaitingList, Has.Count.EqualTo(1));
        Assert.That(_store.WaitingList.Single().MemberId, Is.EqualTo(waiter.Id));
    }

    // ---- CancelReservationAsync ----

    [Test]
    public void CancelReservationAsync_BySelf_WithinOneHour_Throws()
    {
        var member = AddMember();
        var lesson = AddLesson(GroupRoom(), DateTime.UtcNow.AddMinutes(30));
        var res = _store.SeedReservation(lesson, member.Id);

        Assert.ThrowsAsync<DomainException>(() => _service.CancelReservationAsync(res.Id, member.Id));
    }

    [Test]
    public async Task CancelReservationAsync_BySelf_MoreThanOneHourAhead_Cancels()
    {
        var member = AddMember();
        var lesson = AddLesson(GroupRoom(), DateTime.UtcNow.AddHours(3));
        var res = _store.SeedReservation(lesson, member.Id);

        await _service.CancelReservationAsync(res.Id, member.Id);

        Assert.That(res.Status, Is.EqualTo(ReservationStatus.Cancelled));
        Assert.That(_dispatcher.Dispatched.OfType<ReservationCancelledEvent>().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task CancelReservationAsync_ByStaff_WithinOneHour_Cancels()
    {
        var member = AddMember();
        var staff = new UserAccount("emp@test.com", Role.Employee);
        _store.Users.Add(staff);
        var lesson = AddLesson(GroupRoom(), DateTime.UtcNow.AddMinutes(10));
        var res = _store.SeedReservation(lesson, member.Id);

        await _service.CancelReservationAsync(res.Id, staff.Id);

        Assert.That(res.Status, Is.EqualTo(ReservationStatus.Cancelled));
    }

    [Test]
    public void CancelReservationAsync_ByOtherMember_Throws()
    {
        var member = AddMember();
        var other = AddMember();
        var lesson = AddLesson(GroupRoom(), DateTime.UtcNow.AddHours(3));
        var res = _store.SeedReservation(lesson, member.Id);

        Assert.ThrowsAsync<DomainException>(() => _service.CancelReservationAsync(res.Id, other.Id));
    }

    // ---- CheckInAsync ----

    [Test]
    public async Task CheckInAsync_WithinWindow_MarksAttended()
    {
        var member = AddMember();
        var lesson = AddLesson(GroupRoom(), DateTime.UtcNow.AddMinutes(10));
        var res = _store.SeedReservation(lesson, member.Id);

        await _service.CheckInAsync(member.Id, lesson.Id);

        Assert.That(res.Status, Is.EqualTo(ReservationStatus.Attended));
    }

    [Test]
    public void CheckInAsync_TooEarly_Throws()
    {
        var member = AddMember();
        var lesson = AddLesson(GroupRoom(), DateTime.UtcNow.AddHours(2));
        _store.SeedReservation(lesson, member.Id);

        Assert.ThrowsAsync<DomainException>(() => _service.CheckInAsync(member.Id, lesson.Id));
    }

    [Test]
    public void CheckInAsync_AfterLessonEnded_Throws()
    {
        var member = AddMember();
        var lesson = AddLesson(GroupRoom(), DateTime.UtcNow.AddHours(-3));
        _store.SeedReservation(lesson, member.Id);

        Assert.ThrowsAsync<DomainException>(() => _service.CheckInAsync(member.Id, lesson.Id));
    }

    // ---- LeaveWaitlistAsync ----

    [Test]
    public async Task LeaveWaitlistAsync_RemovesEntry()
    {
        var taker = AddMember();
        var waiter = AddMember();
        var lesson = AddLesson(GroupRoom(capacity: 1));
        await _service.ReserveAsync(taker.Id, lesson.Id);
        await _service.ReserveAsync(waiter.Id, lesson.Id);

        await _service.LeaveWaitlistAsync(waiter.Id, lesson.Id);

        Assert.That(_store.WaitingList, Is.Empty);
    }

    private static DateTime ThisMonday()
    {
        // Monday of the current week. Always within (and not beyond) the 1-week reservation window,
        // so three lessons placed Mon/Tue/Wed all fall in the same week and pass the upper-bound check.
        var d = DateTime.UtcNow.Date;
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(-1);
        return d;
    }
}
