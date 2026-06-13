using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.Entities;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Employee")]
    public class LessonsController : ControllerBase
    {
        private readonly ILessonRepository _lessonRepo;
        private readonly IWorkoutRepository _workoutRepo;
        private readonly ILocationRepository _locationRepo;
        private readonly IInstructorRepository _instructorRepo;
        private readonly SportVitaal.Domain.Repositories.IUnitOfWork _uow;

        public LessonsController(ILessonRepository lessonRepo, IWorkoutRepository workoutRepo, ILocationRepository locationRepo, IInstructorRepository instructorRepo, SportVitaal.Domain.Repositories.IUnitOfWork uow)
        {
            _lessonRepo = lessonRepo;
            _workoutRepo = workoutRepo;
            _locationRepo = locationRepo;
            _instructorRepo = instructorRepo;
            _uow = uow;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetRange([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var f = from ?? DateTime.UtcNow.Date;
            var t = to ?? f.AddDays(14);
            var lessons = await _lessonRepo.GetLessonsInRangeAsync(f, t);
            return Ok(lessons);
        }

        // Employee-only (no [AllowAnonymous]): occupancy of past and upcoming lessons.
        [HttpGet("occupancy")]
        public async Task<IActionResult> GetOccupancy([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var f = from ?? DateTime.UtcNow.AddMonths(-3);
            var t = to ?? DateTime.UtcNow.AddMonths(3);
            var lessons = await _lessonRepo.GetForOccupancyAsync(f, t);

            var now = DateTime.UtcNow;
            var result = lessons.Select(l => new
            {
                lessonId = l.Id,
                l.WorkoutId,
                l.StartAt,
                l.DurationMinutes,
                locationId = l.Location.Id,
                locationName = l.Location.Name,
                capacity = l.Capacity,
                reserved = l.Reservations.Count(r => r.Status != ReservationStatus.Cancelled),
                l.InstructorId,
                isPast = l.StartAt < now
            });

            return Ok(result);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(Guid id)
        {
            var lesson = await _lessonRepo.GetByIdAsync(id);
            if (lesson == null) return NotFound();
            return Ok(lesson);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLessonDto dto)
        {
            // Validate workout and location exist
            var workout = await _workoutRepo.GetByIdAsync(dto.WorkoutId);
            if (workout == null) return BadRequest("Workout not found");

            // A null InstructorId is allowed and means "nnb" (nog niet bekend).
            if (dto.InstructorId is { } instructorId && await _instructorRepo.GetByIdAsync(instructorId) == null)
                return BadRequest("Instructor not found");

            try
            {
                var location = await _locationRepo.GetByIdAsync(dto.LocationId);
                var lesson = new Lesson(dto.WorkoutId, dto.StartAt, dto.DurationMinutes, location, dto.InstructorId);
                await _lessonRepo.AddAsync(lesson);
                await _uow.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = lesson.Id }, lesson);
            }
            catch (KeyNotFoundException)
            {
                return BadRequest("Location not found");
            }
        }

        [HttpPost("recurring")]
        public async Task<IActionResult> CreateRecurring([FromBody] CreateRecurringLessonDto dto)
        {
            const int maxOccurrences = 366;

            var workout = await _workoutRepo.GetByIdAsync(dto.WorkoutId);
            if (workout == null) return BadRequest("Workout not found");

            if (dto.InstructorId is { } instructorId && await _instructorRepo.GetByIdAsync(instructorId) == null)
                return BadRequest("Instructor not found");

            if (dto.Interval < 1) return BadRequest("Interval must be at least 1.");

            // Exactly one end condition: an end date OR a fixed number of repetitions.
            var hasUntil = dto.Until.HasValue;
            var hasCount = dto.Count.HasValue;
            if (hasUntil == hasCount) return BadRequest("Provide exactly one of 'until' or 'count'.");
            if (hasUntil && dto.Until!.Value <= dto.StartAt) return BadRequest("'until' must be after the start time.");
            if (hasCount && dto.Count!.Value < 1) return BadRequest("'count' must be at least 1.");

            Location location;
            try
            {
                location = await _locationRepo.GetByIdAsync(dto.LocationId);
            }
            catch (KeyNotFoundException)
            {
                return BadRequest("Location not found");
            }

            // Expand the recurrence into individual lesson rows.
            var occurrences = new List<DateTime>();
            var current = dto.StartAt;
            while (true)
            {
                if (hasCount && occurrences.Count >= dto.Count!.Value) break;
                if (hasUntil && current > dto.Until!.Value) break;

                occurrences.Add(current);
                if (occurrences.Count >= maxOccurrences) break;

                current = dto.Frequency switch
                {
                    RecurrenceFrequency.Daily => current.AddDays(dto.Interval),
                    RecurrenceFrequency.Weekly => current.AddDays(7 * dto.Interval),
                    RecurrenceFrequency.Monthly => current.AddMonths(dto.Interval),
                    _ => throw new ArgumentOutOfRangeException(nameof(dto.Frequency))
                };
            }

            var lessons = occurrences
                .Select(start => new Lesson(dto.WorkoutId, start, dto.DurationMinutes, location, dto.InstructorId))
                .ToList();

            foreach (var lesson in lessons) await _lessonRepo.AddAsync(lesson);
            await _uow.SaveChangesAsync();

            return Ok(new
            {
                created = lessons.Count,
                truncated = lessons.Count >= maxOccurrences,
                lessonIds = lessons.Select(l => l.Id)
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLessonDto dto)
        {
            var lesson = await _lessonRepo.GetByIdAsync(id);
            if (lesson == null) return NotFound();

            // A null InstructorId is allowed and means "nnb" (nog niet bekend).
            if (dto.InstructorId is { } instructorId && await _instructorRepo.GetByIdAsync(instructorId) == null)
                return BadRequest("Instructor not found");

            try
            {
                var location = await _locationRepo.GetByIdAsync(dto.LocationId);
                lesson.Reschedule(dto.StartAt, dto.DurationMinutes, location);
                lesson.ChangeInstructor(dto.InstructorId);
                await _lessonRepo.UpdateAsync(lesson);
                await _uow.SaveChangesAsync();
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return BadRequest("Location not found");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _lessonRepo.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CreateLessonDto
    {
        public Guid WorkoutId { get; set; }
        public Guid LocationId { get; set; }
        public DateTime StartAt { get; set; }
        public int DurationMinutes { get; set; }
        public Guid? InstructorId { get; set; }
    }

    public class UpdateLessonDto
    {
        public Guid LocationId { get; set; }
        public DateTime StartAt { get; set; }
        public int DurationMinutes { get; set; }
        public Guid? InstructorId { get; set; }
    }

    public enum RecurrenceFrequency
    {
        Daily,
        Weekly,
        Monthly
    }

    public class CreateRecurringLessonDto
    {
        public Guid WorkoutId { get; set; }
        public Guid LocationId { get; set; }
        public DateTime StartAt { get; set; }
        public int DurationMinutes { get; set; }
        public Guid? InstructorId { get; set; }

        public RecurrenceFrequency Frequency { get; set; }
        /// <summary>Repeat every N units of the chosen frequency (default 1).</summary>
        public int Interval { get; set; } = 1;
        /// <summary>Inclusive end date. Provide either this or <see cref="Count"/>.</summary>
        public DateTime? Until { get; set; }
        /// <summary>Number of occurrences including the first. Provide either this or <see cref="Until"/>.</summary>
        public int? Count { get; set; }
    }
}

