using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;

namespace SportVitaal.Tests.Domain;

/// <summary>
/// Construction/invariant tests for the simpler domain entities: Workout, Location, Instructor,
/// UserAccount, Reservation and Notification.
/// </summary>
[TestFixture]
public class EntityValidationTests
{
    // ---- Workout ----

    [Test]
    public void Workout_Valid_Trims()
    {
        var w = new Workout("  Yoga  ", 60, "Ontspannend");
        Assert.Multiple(() =>
        {
            Assert.That(w.Name, Is.EqualTo("Yoga"));
            Assert.That(w.DefaultDurationMinutes, Is.EqualTo(60));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Workout_BlankName_Throws(string name)
        => Assert.Throws<DomainException>(() => new Workout(name, 60));

    [Test]
    public void Workout_NonPositiveDuration_Throws()
        => Assert.Throws<DomainException>(() => new Workout("Yoga", 0));

    [Test]
    public void Workout_Update_ChangesValues()
    {
        var w = new Workout("Yoga", 60);
        w.Update("Bodyshape", 45, "Kracht");
        Assert.That(w.Name, Is.EqualTo("Bodyshape"));
        Assert.That(w.DefaultDurationMinutes, Is.EqualTo(45));
    }

    // ---- Location ----

    [Test]
    public void Location_Valid_SetsProperties()
    {
        var l = new Location("Spinningzaal", 24, allowsSeatSelection: true);
        Assert.Multiple(() =>
        {
            Assert.That(l.Capacity, Is.EqualTo(24));
            Assert.That(l.AllowsSeatSelection, Is.True);
        });
    }

    [Test]
    public void Location_NonPositiveCapacity_Throws()
        => Assert.Throws<DomainException>(() => new Location("Zaal", 0));

    // ---- Instructor ----

    [Test]
    public void Instructor_Valid_SetsName()
    {
        var i = new Instructor("Sanne", "/img/sanne.jpg");
        Assert.That(i.Name, Is.EqualTo("Sanne"));
        Assert.That(i.PhotoUrl, Is.EqualTo("/img/sanne.jpg"));
    }

    [Test]
    public void Instructor_BlankName_Throws()
        => Assert.Throws<DomainException>(() => new Instructor(" "));

    [Test]
    public void Instructor_Rename_Trims()
    {
        var i = new Instructor("Sanne");
        i.Rename("  Bram ");
        Assert.That(i.Name, Is.EqualTo("Bram"));
    }

    // ---- UserAccount ----

    [Test]
    public void UserAccount_NormalisesEmail()
    {
        var u = new UserAccount("  Test@Example.COM ", Role.Member);
        Assert.That(u.Email, Is.EqualTo("test@example.com"));
        Assert.That(u.IsActive, Is.True);
    }

    [Test]
    public void UserAccount_BlankEmail_Throws()
        => Assert.Throws<DomainException>(() => new UserAccount(" "));

    [Test]
    public void UserAccount_UpdateProfile_KeepsExistingFullNameWhenBlank()
    {
        var u = new UserAccount("a@b.com");
        u.UpdateProfile("nick", "Full Name", "/p.jpg");
        u.UpdateProfile("nick2", "  ", null); // blank full name should be ignored

        Assert.Multiple(() =>
        {
            Assert.That(u.UserName, Is.EqualTo("nick2"));
            Assert.That(u.FullName, Is.EqualTo("Full Name"));
            Assert.That(u.PhotoUrl, Is.EqualTo("/p.jpg"));
        });
    }

    [Test]
    public void UserAccount_LinkInstructor_OnNonInstructorRole_Throws()
    {
        var u = new UserAccount("m@b.com", Role.Member);
        Assert.Throws<DomainException>(() => u.LinkInstructor(Guid.NewGuid()));
    }

    [Test]
    public void UserAccount_LinkInstructor_OnInstructorRole_Succeeds()
    {
        var u = new UserAccount("i@b.com", Role.Instructor);
        var instructorId = Guid.NewGuid();
        u.LinkInstructor(instructorId);
        Assert.That(u.InstructorId, Is.EqualTo(instructorId));
    }

    [Test]
    public void UserAccount_CancelMembership_WithoutMembership_Throws()
    {
        var u = new UserAccount("m@b.com");
        Assert.Throws<DomainException>(() => u.CancelMembership());
    }

    // ---- Reservation ----

    [Test]
    public void Reservation_Cancel_FreesSeat()
    {
        var r = new Reservation(Guid.NewGuid(), Guid.NewGuid(), seatNumber: 3);
        r.Cancel();
        Assert.Multiple(() =>
        {
            Assert.That(r.Status, Is.EqualTo(ReservationStatus.Cancelled));
            Assert.That(r.SeatNumber, Is.Null);
        });
    }

    // ---- Notification ----

    [Test]
    public void Notification_Valid_IsUnreadUntilMarked()
    {
        var n = new Notification(Guid.NewGuid(), NotificationType.WaitlistSpotAvailable, "Plek vrij", "Er is een plek vrijgekomen.");
        Assert.That(n.IsRead, Is.False);

        n.MarkRead();
        Assert.That(n.IsRead, Is.True);
    }

    [Test]
    public void Notification_MarkRead_IsIdempotent()
    {
        var n = new Notification(Guid.NewGuid(), NotificationType.General, "Titel", "Body");
        n.MarkRead();
        var first = n.ReadAt;
        n.MarkRead();
        Assert.That(n.ReadAt, Is.EqualTo(first));
    }

    [Test]
    public void Notification_EmptyUser_Throws()
        => Assert.Throws<DomainException>(() => new Notification(Guid.Empty, NotificationType.General, "T", "B"));

    [Test]
    public void Notification_BlankTitle_Throws()
        => Assert.Throws<DomainException>(() => new Notification(Guid.NewGuid(), NotificationType.General, " ", "B"));
}
