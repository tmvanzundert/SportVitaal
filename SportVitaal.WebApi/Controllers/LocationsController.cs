using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.Entities;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationsController : ControllerBase
    {
        private readonly ILocationRepository _repo;
        private readonly SportVitaal.Domain.Repositories.IUnitOfWork _uow;

        public LocationsController(ILocationRepository repo, SportVitaal.Domain.Repositories.IUnitOfWork uow)
        {
            _repo = repo;
            _uow = uow;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _repo.GetAllAsync();
            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            try
            {
                var l = await _repo.GetByIdAsync(id);
                return Ok(l);
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLocationDto dto)
        {
            var loc = new Location(dto.Name, dto.Capacity, dto.AllowsSeatSelection);
            await _repo.AddAsync(loc);
            await _uow.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = loc.Id }, loc);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateLocationDto dto)
        {
            try
            {
                var loc = await _repo.GetByIdAsync(id);
                loc.Update(dto.Name, dto.Capacity, dto.AllowsSeatSelection);
                await _repo.UpdateAsync(loc);
                await _uow.SaveChangesAsync();
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var loc = await _repo.GetByIdAsync(id);
                await _repo.RemoveAsync(loc);
                await _uow.SaveChangesAsync();
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }
    }

    public class CreateLocationDto
    {
        public string Name { get; set; } = null!;
        public int Capacity { get; set; }
        public bool AllowsSeatSelection { get; set; }
    }
}

