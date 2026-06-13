using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.Entities;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Employee")]
    public class WorkoutsController : ControllerBase
    {
        private readonly IWorkoutRepository _repo;
        private readonly ILessonRepository _lessonRepo;
        private readonly SportVitaal.Domain.Repositories.IUnitOfWork _uow;

        public WorkoutsController(IWorkoutRepository repo, ILessonRepository lessonRepo, SportVitaal.Domain.Repositories.IUnitOfWork uow)
        {
            _repo = repo;
            _lessonRepo = lessonRepo;
            _uow = uow;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var items = await _repo.GetAllAsync();
            return Ok(items);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(Guid id)
        {
            var w = await _repo.GetByIdAsync(id);
            if (w == null) return NotFound();
            return Ok(w);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWorkoutDto dto)
        {
            var w = new Workout(dto.Name, dto.DefaultDurationMinutes, dto.Description);
            await _repo.AddAsync(w);
            await _uow.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = w.Id }, w);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateWorkoutDto dto)
        {
            var w = await _repo.GetByIdAsync(id);
            if (w == null) return NotFound();
            w.Update(dto.Name, dto.DefaultDurationMinutes, dto.Description);
            await _repo.UpdateAsync(w);
            await _uow.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            // Deleting a workout should also remove all lessons scheduled for it.
            // Makes each lesson's reservations and waiting-list entries cascade away via EF.
            var lessons = await _lessonRepo.GetByWorkoutIdAsync(id);
            foreach (var lesson in lessons)
                await _lessonRepo.DeleteAsync(lesson.Id);

            await _repo.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CreateWorkoutDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int DefaultDurationMinutes { get; set; }
    }
}


