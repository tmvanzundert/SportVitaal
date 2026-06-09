using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;

namespace SportVitaal.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Lesson> Lessons { get; set; } = null!;
        public DbSet<Reservation> Reservations { get; set; } = null!;
        public DbSet<UserAccount> Users { get; set; } = null!;
        public DbSet<Workout> Workouts { get; set; } = null!;
        public DbSet<Location> Locations { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Keys
            modelBuilder.Entity<Lesson>().HasKey(l => l.Id);
            modelBuilder.Entity<Reservation>().HasKey(r => r.Id);

            // Map Lesson -> Reservations using the public Reservations property instead of explicitly mapping the backing field
            modelBuilder.Entity<Lesson>()
                .HasMany(l => l.Reservations)
                .WithOne()
                .HasForeignKey("LessonId")
                .OnDelete(DeleteBehavior.Cascade);

            // Map Lesson -> WaitingList using the public WaitingList property so waiting entries are persisted
            modelBuilder.Entity<Lesson>()
                .HasMany(l => l.WaitingList)
                .WithOne()
                .HasForeignKey("LessonId")
                .OnDelete(DeleteBehavior.Cascade);

            // Unique seat per lesson (allow multiple NULLs for SeatNumber)
            modelBuilder.Entity<Reservation>()
                .HasIndex(r => new { r.LessonId, r.SeatNumber })
                .IsUnique();

            // Map other entities as simple keyed tables
            modelBuilder.Entity<UserAccount>().HasKey(u => u.Id);
            // Map Membership as an owned/value object inside UserAccount
            modelBuilder.Entity<UserAccount>().OwnsOne(u => u.Membership, m =>
            {
                m.Property(mm => mm.Type).HasColumnName("MembershipType");
                m.Property(mm => mm.StartDate).HasColumnName("MembershipStartDate");
                m.Property(mm => mm.EndDate).HasColumnName("MembershipEndDate");
            });
            modelBuilder.Entity<Workout>().HasKey(w => w.Id);
            modelBuilder.Entity<Location>().HasKey(l => l.Id);
        }
    }
}
