using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportVitaal.Application.Services;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;
using SportVitaal.Domain.Repositories;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Employee")]
    public class InstructorsController : ControllerBase
    {
        private static readonly HashSet<string> AllowedPhotoExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxPhotoBytes = 5 * 1024 * 1024; // 5 MB

        private readonly IInstructorRepository _repo;
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _uow;
        private readonly IWebHostEnvironment _env;

        public InstructorsController(IInstructorRepository repo, IUserRepository users, IUnitOfWork uow, IWebHostEnvironment env)
        {
            _repo = repo;
            _users = users;
            _uow = uow;
            _env = env;
        }

        // Instructors are part of the public-facing schedule, so listing them is open.
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
            var instructor = await _repo.GetByIdAsync(id);
            if (instructor == null) return NotFound();
            return Ok(instructor);
        }

        // Adding an instructor provisions a full login account with the Instructor role; the
        // Instructor entity remains the scheduling identity that lessons are booked against, and
        // the account is linked to it so the instructor can sign in and resolve their own lessons.
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInstructorDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest("E-mailadres is verplicht.");
            if (string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Wachtwoord is verplicht.");

            var existing = await _users.GetByEmailAsync(dto.Email);
            if (existing != null)
                return Conflict("Er bestaat al een gebruiker met dit e-mailadres.");

            var instructor = new Instructor(dto.Name, dto.PhotoUrl);
            await _repo.AddAsync(instructor);

            var user = new UserAccount(dto.Email, Role.Instructor);
            user.UpdateProfile(null, dto.Name, null);
            user.SetPasswordHash(PasswordHasher.HashPassword(dto.Password));
            user.LinkInstructor(instructor.Id);
            await _users.AddAsync(user);

            await _uow.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = instructor.Id }, instructor);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] InstructorDto dto)
        {
            var instructor = await _repo.GetByIdAsync(id);
            if (instructor == null) return NotFound();

            instructor.Rename(dto.Name);
            if (dto.PhotoUrl != null) instructor.SetPhoto(dto.PhotoUrl);
            await _repo.UpdateAsync(instructor);

            // Keep the linked login account's display name in sync with the instructor name.
            var account = await FindAccountAsync(id);
            if (account != null)
            {
                account.UpdateProfile(null, dto.Name, null);
                await _users.UpdateAsync(account);
            }

            await _uow.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/photo")]
        [RequestSizeLimit(MaxPhotoBytes)]
        public async Task<IActionResult> UploadPhoto(Guid id, IFormFile file)
        {
            var instructor = await _repo.GetByIdAsync(id);
            if (instructor == null) return NotFound();

            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
            if (file.Length > MaxPhotoBytes) return BadRequest("File exceeds the 5 MB limit.");

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedPhotoExtensions.Contains(ext))
                return BadRequest("Unsupported image type. Allowed: jpg, jpeg, png, webp.");

            // Never trust the client-supplied filename; generate a safe one.
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var relativeDir = Path.Combine("uploads", "instructors");
            var targetDir = Path.Combine(webRoot, relativeDir);
            Directory.CreateDirectory(targetDir);

            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(targetDir, fileName);
            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            // Store a web-relative URL (forward slashes) so the front-end can render it directly.
            var photoUrl = "/" + Path.Combine(relativeDir, fileName).Replace(Path.DirectorySeparatorChar, '/');
            instructor.SetPhoto(photoUrl);
            await _repo.UpdateAsync(instructor);
            await _uow.SaveChangesAsync();

            return Ok(new { photoUrl });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            // Remove the linked login account along with the scheduling identity so we don't leave
            // an orphaned instructor account behind.
            var account = await FindAccountAsync(id);
            if (account != null) await _users.DeleteAsync(account.Id);

            await _repo.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            return NoContent();
        }

        private Task<UserAccount?> FindAccountAsync(Guid instructorId)
            => _users.GetByInstructorIdAsync(instructorId);
    }

    public class InstructorDto
    {
        public string Name { get; set; } = null!;
        public string? PhotoUrl { get; set; }
    }

    public class CreateInstructorDto
    {
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? PhotoUrl { get; set; }
    }
}
