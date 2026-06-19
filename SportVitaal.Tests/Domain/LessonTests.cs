using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.Entities;

namespace SportVitaal.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="Lesson"/> aggregate: reserving, capacity/waiting-list handling,
/// seat selection (spinning bikes), check-in and cancellation.
/// </summary>
[TestFixture]
public class LessonTests
{
    private static Location GroupRoom(int capacity = 3) => new("Zaal", capacity);
    private static Location SpinningRoom(int capacity = 4) => new("Spinning", capacity, allowsSeatSelection: true);

    private static Lesson NewLesson(Location location, DateTime? startAt = null)
        => new(Guid.NewGuid(), startAt ?? DateTime.UtcNow.AddDays(1), 60, location);

    // ---- Construction (good & bad) ----

    [Test]
    public void Constructor_WithValidArguments_SetsProperties()
    {
        var start = DateTime.UtcNow.AddDays(2);
        var lesson = new Lesson(Guid.NewGuid(), start, 45, GroupRoom(42));

        Assert.Multiple(() =>
        {
            Assert.That(lesson.StartAt, Is.EqualTo(start));
            Assert.That(lesson.DurationMinutes, Is.EqualTo(45));
            Assert.That(lesson.Capacity, Is.EqualTo(42));
            Assert.That(lesson.Reservations, Is.Empty);
        });
    }

    [Test]
    public void Constructor_WithNonPositiveDuration_Throws()
        => Assert.Throws<DomainException>(() => new Lesson(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 0, GroupRoom()));

    [Test]
    public void Constructor_WithNullLocation_Throws()
        => Assert.Throws<ArgumentNullException>(() => new Lesson(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 60, null!));

    // ---- Reserve: happy path ----

    [Test]
    public void Reserve_WhenSpaceAvailable_CreatesReservedReservation()
    {
        var lesson = NewLesson(GroupRoom());
        var member = Guid.NewGuid();

        var reservation = lesson.Reserve(member);

        Assert.That(reservation, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(reservation!.MemberId, Is.EqualTo(member));
            Assert.That(reservation.Status, Is.EqualTo(ReservationStatus.Reserved));
            Assert.That(lesson.Reservations, Has.Count.EqualTo(1));
        });
    }

    // ---- Reserve: bad flows ----

    [Test]
    public void Reserve_SameMemberTwice_Throws()
    {
        var lesson = NewLesson(GroupRoom());
        var member = Guid.NewGuid();
        lesson.Reserve(member);

        Assert.Throws<DomainException>(() => lesson.Reserve(member));
    }

    [Test]
    public void Reserve_WhenFull_AddsToWaitingListAndReturnsNull()
    {
        var lesson = NewLesson(GroupRoom(capacity: 1));
        lesson.Reserve(Guid.NewGuid()); // fills the only spot

        var waiter = Guid.NewGuid();
        var result = lesson.Reserve(waiter);

        Assert.That(result, Is.Null);
        Assert.That(lesson.WaitingList, Has.Count.EqualTo(1));
        Assert.That(lesson.WaitingList.First().MemberId, Is.EqualTo(waiter));
    }

    [Test]
    public void Reserve_SameMemberOnWaitingListTwice_Throws()
    {
        var lesson = NewLesson(GroupRoom(capacity: 1));
        lesson.Reserve(Guid.NewGuid());
        var waiter = Guid.NewGuid();
        lesson.Reserve(waiter); // first time -> waiting list

        Assert.Throws<DomainException>(() => lesson.Reserve(waiter));
    }

    [Test]
    public void Reserve_AfterAFreedSpot_DoesNotCountCancelledTowardCapacity()
    {
        var lesson = NewLesson(GroupRoom(capacity: 1));
        var first = lesson.Reserve(Guid.NewGuid())!;
        lesson.CancelReservation(first.Id);

        var second = lesson.Reserve(Guid.NewGuid());

        Assert.That(second, Is.Not.Null);
    }

    // ---- Seat selection (spinning) ----

    [Test]
    public void Reserve_WithSeat_OnSeatSelectableLocation_Succeeds()
    {
        var lesson = NewLesson(SpinningRoom(capacity: 24));

        var reservation = lesson.Reserve(Guid.NewGuid(), seatNumber: 7);

        Assert.That(reservation!.SeatNumber, Is.EqualTo(7));
    }

    [Test]
    public void Reserve_WithSeat_OnNonSeatLocation_Throws()
    {
        var lesson = NewLesson(GroupRoom());
        Assert.Throws<DomainException>(() => lesson.Reserve(Guid.NewGuid(), seatNumber: 1));
    }

    [Test]
    public void Reserve_WithSeatOutOfRange_Throws()
    {
        var lesson = NewLesson(SpinningRoom(capacity: 24));
        Assert.Throws<DomainException>(() => lesson.Reserve(Guid.NewGuid(), seatNumber: 25));
    }

    [Test]
    public void Reserve_WithAlreadyTakenSeat_Throws()
    {
        var lesson = NewLesson(SpinningRoom(capacity: 24));
        lesson.Reserve(Guid.NewGuid(), seatNumber: 5);

        Assert.Throws<DomainException>(() => lesson.Reserve(Guid.NewGuid(), seatNumber: 5));
    }

    [Test]
    public void Reserve_SeatFreedByCancellation_CanBeTakenAgain()
    {
        var lesson = NewLesson(SpinningRoom(capacity: 24));
        var first = lesson.Reserve(Guid.NewGuid(), seatNumber: 5)!;
        lesson.CancelReservation(first.Id);

        var second = lesson.Reserve(Guid.NewGuid(), seatNumber: 5);

        Assert.That(second!.SeatNumber, Is.EqualTo(5));
    }

    // ---- Check-in ----

    [Test]
    public void CheckIn_WithActiveReservation_MarksAttended()
    {
        var lesson = NewLesson(GroupRoom());
        var member = Guid.NewGuid();
        lesson.Reserve(member);

        var res = lesson.CheckIn(member);

        Assert.That(res.Status, Is.EqualTo(ReservationStatus.Attended));
    }

    [Test]
    public void CheckIn_WithoutReservation_Throws()
    {
        var lesson = NewLesson(GroupRoom());
        Assert.Throws<DomainException>(() => lesson.CheckIn(Guid.NewGuid()));
    }

    // ---- Cancellation & instructor ----

    [Test]
    public void CancelReservation_UnknownId_Throws()
    {
        var lesson = NewLesson(GroupRoom());
        Assert.Throws<DomainException>(() => lesson.CancelReservation(Guid.NewGuid()));
    }

    [Test]
    public void ChangeInstructor_ToNull_RepresentsNnb()
    {
        var lesson = new Lesson(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 60, GroupRoom(), Guid.NewGuid());

        lesson.ChangeInstructor(null);

        Assert.That(lesson.InstructorId, Is.Null);
    }
}
