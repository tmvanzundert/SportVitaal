using Microsoft.EntityFrameworkCore;
using SportVitaal.Application.Services;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;

namespace SportVitaal.Infrastructure.Data
{
    public static class SeedData
    {
        public static async Task EnsureSeedDataAsync(AppDbContext db)
        {
            await EnsureWorkoutsAndLocationsAsync(db);
            await EnsureInstructorsAsync(db);
            await EnsureEmployeesAsync(db);
            await EnsureInstructorAccountsAsync(db);
            await EnsureMembersAsync(db);
            await EnsureScheduleAsync(db);
        }

        private static async Task EnsureWorkoutsAndLocationsAsync(AppDbContext db)
        {
            // Workouts
            if (!db.Workouts.Any())
            {
                var w1 = new Workout("Yoga", 60, "Relaxing yoga flow");
                var w2 = new Workout("Spinning", 45, "High intensity spinning");
                var w3 = new Workout("Bootcamp", 50, "Outdoor bootcamp");
                await db.Workouts.AddRangeAsync(new[] { w1, w2, w3 });
            }

            // Locations
            if (!db.Locations.Any())
            {
                var l1 = new Location("Zaal 1", 42, false);
                var l2 = new Location("Zaal 2", 32, false);
                var l3 = new Location("Zaal 3", 24, false);
                var outside = new Location("Buitenruimte", 20, false);
                var spinning = new Location("Spinningruimte", 24, true);
                await db.Locations.AddRangeAsync(new[] { l1, l2, l3, outside, spinning });
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureInstructorsAsync(AppDbContext db)
        {
            if (db.Instructors.Any()) return;

            var instructors = new[]
            {
                new Instructor("Marit Jansen"),
                new Instructor("Bram Hendriks"),
                new Instructor("Fatima El Amrani"),
                new Instructor("Kevin de Boer"),
            };
            await db.Instructors.AddRangeAsync(instructors);
            await db.SaveChangesAsync();
        }

        private static async Task EnsureEmployeesAsync(AppDbContext db)
        {
            if (db.Users.Any(u => u.Role == Role.Employee)) return;

            // Two admin (employee) accounts for the beheer dashboard. Password: "Admin123!".
            var employees = new[]
            {
                CreateUser("admin@sportvitaal.nl", "Sophie Beheerder", Role.Employee, "Admin123!"),
                CreateUser("manager@sportvitaal.nl", "Pieter Manager", Role.Employee, "Admin123!"),
            };
            await db.Users.AddRangeAsync(employees);
            await db.SaveChangesAsync();
        }

        private static async Task EnsureInstructorAccountsAsync(AppDbContext db)
        {
            var instructors = db.Instructors.ToList();

            // Login accounts for the seeded instructors so they can use the app. Each account is
            // linked to its Instructor identity (matched by name) so the app can resolve the
            // instructor's own lessons. Password: "Docent123!". Idempotent: creates any missing
            // account and backfills the instructor link if it is not set yet.
            var specs = new[]
            {
                ("marit@sportvitaal.nl", "Marit Jansen"),
                ("bram@sportvitaal.nl", "Bram Hendriks"),
                ("fatima@sportvitaal.nl", "Fatima El Amrani"),
                ("kevin@sportvitaal.nl", "Kevin de Boer"),
            };

            var changed = false;
            foreach (var (email, name) in specs)
            {
                var user = db.Users.FirstOrDefault(u => u.Email == email);
                if (user is null)
                {
                    user = CreateUser(email, name, Role.Instructor, "Docent123!");
                    await db.Users.AddAsync(user);
                    changed = true;
                }

                if (user.Role == Role.Instructor && user.InstructorId is null)
                {
                    var instructor = instructors.FirstOrDefault(i => i.Name == name);
                    if (instructor != null)
                    {
                        user.LinkInstructor(instructor.Id);
                        changed = true;
                    }
                }
            }

            if (changed) await db.SaveChangesAsync();
        }

        private static async Task EnsureMembersAsync(AppDbContext db)
        {
            if (db.Users.Any(u => u.Role == Role.Member)) return;

            var today = DateTime.UtcNow.Date;

            // Ten members with a mix of subscription types and states. Password: "Member123!".
            // UserName is the member-visible name shown on the lesson detail screen.
            var specs = new (string Email, string FullName, string UserName, MembershipType? Type, int StartMonthsAgo, bool Expired)[]
            {
                ("sanne.devries@example.com",   "Sanne de Vries",   "SanneDV",     MembershipType.UnlimitedYearly,     4,  false),
                ("tom.bakker@example.com",      "Tom Bakker",       "TomB",        MembershipType.TwiceWeeklyMonthly,  2,  false),
                ("lisa.smit@example.com",       "Lisa Smit",        "LisaS",       MembershipType.UnlimitedMonthly,    1,  false),
                ("daan.visser@example.com",     "Daan Visser",      "DaanV",       MembershipType.TwiceWeeklyYearly,   6,  false),
                ("eva.mulder@example.com",      "Eva Mulder",       "EvaM",        MembershipType.UnlimitedYearly,     9,  false),
                ("noah.meijer@example.com",     "Noah Meijer",      "NoahM",       MembershipType.TwiceWeeklyMonthly,  3,  false),
                ("julia.bos@example.com",       "Julia Bos",        "JuliaB",      MembershipType.UnlimitedMonthly,    5,  false),
                ("lucas.vermeulen@example.com", "Lucas Vermeulen",  "LucasV",      MembershipType.TwiceWeeklyYearly,   11, false),
                ("emma.peeters@example.com",    "Emma Peeters",     "EmmaP",       MembershipType.TwiceWeeklyMonthly,  14, true),  // verlopen
                ("sem.dijkstra@example.com",    "Sem Dijkstra",     "SemD",        null,                               0,  false), // geen abonnement
            };

            foreach (var spec in specs)
            {
                var user = CreateUser(spec.Email, spec.FullName, Role.Member, "Member123!");
                user.UpdateProfile(spec.UserName, null, null);

                if (spec.Type is { } type)
                {
                    var start = today.AddMonths(-spec.StartMonthsAgo);
                    DateTime? end = type switch
                    {
                        // Yearly subscriptions run a fixed year from the start date.
                        MembershipType.TwiceWeeklyYearly or MembershipType.UnlimitedYearly => start.AddYears(1),
                        // Monthly subscriptions are open-ended unless marked expired below.
                        _ => null,
                    };

                    if (spec.Expired)
                        end = today.AddMonths(-1);

                    user.StartMembership(new Membership(type, start, end));
                }

                await db.Users.AddAsync(user);
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureScheduleAsync(AppDbContext db)
        {
            if (db.Lessons.Any()) return;

            var workouts = db.Workouts.ToDictionary(w => w.Name, w => w);
            var locations = db.Locations.ToDictionary(l => l.Name, l => l);
            var instructors = db.Instructors.ToList();
            var members = db.Users.Where(u => u.Role == Role.Member).ToList();
            if (workouts.Count == 0 || locations.Count == 0) return;

            // Recurring weekly schedule template: (weekday, time, workout, location).
            var template = new (DayOfWeek Day, int Hour, int Minute, string Workout, string Location)[]
            {
                (DayOfWeek.Monday,    18, 0,  "Spinning", "Spinningruimte"),
                (DayOfWeek.Monday,    19, 30, "Yoga",     "Zaal 1"),
                (DayOfWeek.Tuesday,   18, 30, "Bootcamp", "Buitenruimte"),
                (DayOfWeek.Wednesday, 18, 0,  "Spinning", "Spinningruimte"),
                (DayOfWeek.Wednesday, 19, 0,  "Yoga",     "Zaal 2"),
                (DayOfWeek.Thursday,  18, 30, "Bootcamp", "Buitenruimte"),
                (DayOfWeek.Saturday,  9,  0,  "Yoga",     "Zaal 1"),
                (DayOfWeek.Saturday,  10, 30, "Spinning", "Spinningruimte"),
                (DayOfWeek.Saturday,  11, 0,  "Bootcamp", "Buitenruimte"),
            };

            // Start a month back so the occupancy dashboard shows past lessons too,
            // and run the schedule through to the end of 2026.
            var firstDay = DateTime.UtcNow.Date.AddMonths(-1);
            var lastDay = new DateTime(2026, 12, 31);
            // Lessons are stored in UTC. The template times are the club's local wall-clock
            // times, so convert them through the club zone (DST-aware) rather than tagging UTC.
            var clubZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
            var rng = new Random(20260614);
            var lessons = new List<Lesson>();
            var instructorIndex = 0;

            for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
            {
                foreach (var slot in template.Where(s => s.Day == day.DayOfWeek))
                {
                    if (!workouts.TryGetValue(slot.Workout, out var workout)) continue;
                    if (!locations.TryGetValue(slot.Location, out var location)) continue;

                    var localStart = DateTime.SpecifyKind(
                        day.AddHours(slot.Hour).AddMinutes(slot.Minute), DateTimeKind.Unspecified);
                    var startAt = TimeZoneInfo.ConvertTimeToUtc(localStart, clubZone);

                    Guid? instructorId = instructors.Count == 0
                        ? null
                        : instructors[instructorIndex++ % instructors.Count].Id;

                    var lesson = new Lesson(workout.Id, startAt, workout.DefaultDurationMinutes, location, instructorId);

                    // Populate some reservations so the occupancy dashboard is meaningful.
                    // In seat-selection rooms (e.g. Spinningruimte) hand out distinct seats so the
                    // seat map is realistic; other rooms leave the seat unset.
                    if (members.Count > 0)
                    {
                        var count = rng.Next(0, members.Count + 1);
                        var seat = 1;
                        foreach (var member in members.OrderBy(_ => rng.Next()).Take(count))
                        {
                            int? seatNumber = location.AllowsSeatSelection ? seat++ : null;
                            lesson.Reserve(member.Id, seatNumber);
                        }
                    }

                    lessons.Add(lesson);
                }
            }

            await db.Lessons.AddRangeAsync(lessons);
            await db.SaveChangesAsync();
        }

        private static UserAccount CreateUser(string email, string fullName, Role role, string password)
        {
            var user = new UserAccount(email, role);
            user.UpdateProfile(null, fullName, null);
            user.SetPasswordHash(PasswordHasher.HashPassword(password));
            return user;
        }
    }
}
