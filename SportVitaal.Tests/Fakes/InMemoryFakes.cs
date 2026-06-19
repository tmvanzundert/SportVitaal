using SportVitaal.Domain.DomainEvents;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;
using SportVitaal.Domain.Repositories;

namespace SportVitaal.Tests.Fakes;

/// <summary>
/// A single shared in-memory store the fake repositories read/write, so an aggregate loaded
/// through one repository is the same instance seen through another (mirrors EF change tracking
/// closely enough for service-level tests). Hand-rolled to keep the test project dependency-free.
/// </summary>
public sealed class InMemoryStore
{
    public readonly List<UserAccount> Users = new();
    public readonly List<Lesson> Lessons = new();
    public readonly List<Reservation> Reservations = new();
    public readonly List<WaitingListEntry> WaitingList = new();

    public int SaveChangesCount { get; private set; }
    public void RecordSave() => SaveChangesCount++;

    /// <summary>Adds a reservation to a lesson aggregate and registers it in the store.</summary>
    public Reservation SeedReservation(Lesson lesson, Guid memberId, int? seat = null)
    {
        var res = lesson.Reserve(memberId, seat)
                  ?? throw new InvalidOperationException("Lesson was full; cannot seed a reservation.");
        Reservations.Add(res);
        return res;
    }
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly InMemoryStore _store;
    public FakeUnitOfWork(InMemoryStore store) => _store = store;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        _store.RecordSave();
        return Task.FromResult(1);
    }
}

public sealed class FakeUserRepository : IUserRepository
{
    private readonly InMemoryStore _store;
    public FakeUserRepository(InMemoryStore store) => _store = store;

    public Task<UserAccount?> GetByIdAsync(Guid id)
        => Task.FromResult(_store.Users.FirstOrDefault(u => u.Id == id));

    public Task<UserAccount?> GetByEmailAsync(string email)
        => Task.FromResult(_store.Users.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<UserAccount?> GetByInstructorIdAsync(Guid instructorId)
        => Task.FromResult(_store.Users.FirstOrDefault(u => u.InstructorId == instructorId));

    public Task<IEnumerable<UserAccount>> GetByRoleAsync(Role role)
        => Task.FromResult(_store.Users.Where(u => u.Role == role));

    public Task AddAsync(UserAccount user)
    {
        _store.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _store.Users.RemoveAll(u => u.Id == id);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserAccount user)
    {
        if (!_store.Users.Contains(user)) _store.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task StartMembershipAsync(Guid userId, Membership membership, CancellationToken ct = default)
    {
        _store.Users.First(u => u.Id == userId).StartMembership(membership);
        return Task.CompletedTask;
    }

    public Task CancelMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        _store.Users.First(u => u.Id == userId).CancelMembership();
        return Task.CompletedTask;
    }
}

public sealed class FakeLessonRepository : ILessonRepository
{
    private readonly InMemoryStore _store;
    public FakeLessonRepository(InMemoryStore store) => _store = store;

    public Task<Lesson?> GetByIdAsync(Guid id)
        => Task.FromResult(_store.Lessons.FirstOrDefault(l => l.Id == id));

    public Task<IEnumerable<Lesson>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var set = ids.ToHashSet();
        return Task.FromResult(_store.Lessons.Where(l => set.Contains(l.Id)));
    }

    public Task<IEnumerable<Lesson>> GetLessonsInRangeAsync(DateTime from, DateTime to)
        => Task.FromResult(_store.Lessons.Where(l => l.StartAt >= from && l.StartAt <= to));

    public Task<IEnumerable<Lesson>> GetByWorkoutIdAsync(Guid workoutId)
        => Task.FromResult(_store.Lessons.Where(l => l.WorkoutId == workoutId));

    public Task<IEnumerable<Lesson>> GetForInstructorAsync(Guid instructorId, DateTime from, DateTime to)
        => Task.FromResult(_store.Lessons.Where(l => l.InstructorId == instructorId && l.StartAt >= from && l.StartAt <= to));

    public Task<IEnumerable<Lesson>> GetForOccupancyAsync(DateTime from, DateTime to)
        => Task.FromResult(_store.Lessons.Where(l => l.StartAt >= from && l.StartAt <= to));

    public Task AddAsync(Lesson lesson)
    {
        _store.Lessons.Add(lesson);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Lesson lesson)
    {
        if (!_store.Lessons.Contains(lesson)) _store.Lessons.Add(lesson);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _store.Lessons.RemoveAll(l => l.Id == id);
        return Task.CompletedTask;
    }
}

public sealed class FakeReservationRepository : IReservationRepository
{
    private readonly InMemoryStore _store;
    public FakeReservationRepository(InMemoryStore store) => _store = store;

    public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.Reservations.FirstOrDefault(r => r.Id == id));

    public Task<IEnumerable<Reservation>> GetForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(_store.Reservations.Where(r => r.MemberId == userId));

    public Task<IEnumerable<Reservation>> GetForLessonAsync(Guid lessonId, CancellationToken ct = default)
        => Task.FromResult(_store.Reservations.Where(r => r.LessonId == lessonId));

    public Task<int> CountReservationsForUserInRangeAsync(Guid userId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        // Mirror the real query: count the user's Reserved reservations whose lesson starts in range.
        var lessonStarts = _store.Lessons.ToDictionary(l => l.Id, l => l.StartAt);
        var count = _store.Reservations.Count(r =>
            r.MemberId == userId
            && r.Status == ReservationStatus.Reserved
            && lessonStarts.TryGetValue(r.LessonId, out var start)
            && start >= from && start <= to);
        return Task.FromResult(count);
    }

    public Task AddAsync(Reservation reservation, CancellationToken ct = default)
    {
        if (!_store.Reservations.Contains(reservation)) _store.Reservations.Add(reservation);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Reservation reservation, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoveAsync(Reservation reservation, CancellationToken ct = default)
    {
        _store.Reservations.Remove(reservation);
        return Task.CompletedTask;
    }
}

public sealed class FakeWaitingListRepository : IWaitingListRepository
{
    private readonly InMemoryStore _store;
    public FakeWaitingListRepository(InMemoryStore store) => _store = store;

    public Task<IEnumerable<WaitingListEntry>> GetForLessonAsync(Guid lessonId, CancellationToken ct = default)
        => Task.FromResult(_store.WaitingList.Where(w => w.LessonId == lessonId));

    public Task<IEnumerable<WaitingListEntry>> GetForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(_store.WaitingList.Where(w => w.MemberId == userId));

    public Task AddAsync(WaitingListEntry entry, CancellationToken ct = default)
    {
        if (!_store.WaitingList.Contains(entry)) _store.WaitingList.Add(entry);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(WaitingListEntry entry, CancellationToken ct = default)
    {
        _store.WaitingList.Remove(entry);
        return Task.CompletedTask;
    }
}

public sealed class FakeMembershipRepository : IMembershipRepository
{
    private readonly InMemoryStore _store;
    public FakeMembershipRepository(InMemoryStore store) => _store = store;

    public Task<Membership?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(_store.Users.FirstOrDefault(u => u.Id == userId)?.Membership);

    public Task AddOrUpdateAsync(Guid userId, Membership membership, CancellationToken ct = default)
    {
        _store.Users.First(u => u.Id == userId).StartMembership(membership);
        return Task.CompletedTask;
    }
}

/// <summary>Records dispatched domain events so tests can assert which were raised.</summary>
public sealed class RecordingDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = new();

    public Task DispatchAsync(IDomainEvent domainEvent)
    {
        Dispatched.Add(domainEvent);
        return Task.CompletedTask;
    }
}
