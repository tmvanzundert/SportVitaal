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
        private readonly SportVitaal.Domain.Repositories.IUnitOfWork _uow;

        public LessonsController(ILessonRepository lessonRepo, IWorkoutRepository workoutRepo, ILocationRepository locationRepo, SportVitaal.Domain.Repositories.IUnitOfWork uow)
        {
            _lessonRepo = lessonRepo;
            _workoutRepo = workoutRepo;
            _locationRepo = locationRepo;
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
}

