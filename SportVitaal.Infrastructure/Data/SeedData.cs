using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;

namespace SportVitaal.Infrastructure.Data
{
    public static class SeedData
    {
        public static async Task EnsureSeedDataAsync(AppDbContext db)
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
    }
}


