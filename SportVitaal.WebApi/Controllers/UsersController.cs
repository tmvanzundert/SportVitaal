using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.Repositories;
using SportVitaal.Application.Services;
using SportVitaal.Domain.ValueObjects;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private static readonly HashSet<string> AllowedPhotoExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxPhotoBytes = 5 * 1024 * 1024; // 5 MB

        private readonly IUserRepository _userRepo;
        private readonly SportVitaal.Domain.Repositories.IUnitOfWork _uow;
        private readonly IWebHostEnvironment _env;

        public UsersController(IUserRepository userRepo, SportVitaal.Domain.Repositories.IUnitOfWork uow, IWebHostEnvironment env)
        {
            _userRepo = userRepo;
            _uow = uow;
            _env = env;
        }

        // Employees can see all members and their membership status.
        [HttpGet("members")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> GetMembers()
        {
            var members = await _userRepo.GetByRoleAsync(SportVitaal.Domain.Enums.Role.Member);
            var result = members.Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.UserName,
                Role = u.Role.ToString(),
                Membership = u.Membership == null ? null : new
                {
                    Type = u.Membership.Type.ToString(),
                    u.Membership.StartDate,
                    u.Membership.EndDate,
                    u.Membership.IsActive
                }
            });
            return Ok(result);
        }

        // Employees can remove a member account.
        [HttpDelete("members/{id}")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> DeleteMember(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null) return NotFound();
            if (user.Role != SportVitaal.Domain.Enums.Role.Member)
                return BadRequest("Only member accounts can be removed here.");

            await _userRepo.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
            var user = await _userRepo.GetByIdAsync(Guid.Parse(id));
            if (user == null) return NotFound();
            return Ok(new
            {
                user.Id,
                user.Email,
                user.UserName,
                user.FullName,
                user.PhotoUrl,
                Role = user.Role.ToString(),
                Membership = user.Membership == null ? null : new
                {
                    Type = user.Membership.Type.ToString(),
                    user.Membership.StartDate,
                    user.Membership.EndDate,
                    user.Membership.IsActive
                }
            });
        }

        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
            var user = await _userRepo.GetByIdAsync(Guid.Parse(id));
            if (user == null) return NotFound();

            user.UpdateProfile(dto.UserName, dto.FullName, dto.PhotoUrl);
            await _userRepo.UpdateAsync(user);
            await _uow.SaveChangesAsync();
            return NoContent();
        }

        // Uploads a new profile photo for the signed-in member and stores its web-relative URL.
        [HttpPost("me/photo")]
        [Authorize]
        [RequestSizeLimit(MaxPhotoBytes)]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
            var user = await _userRepo.GetByIdAsync(Guid.Parse(id));
            if (user == null) return NotFound();

            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
            if (file.Length > MaxPhotoBytes) return BadRequest("File exceeds the 5 MB limit.");

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedPhotoExtensions.Contains(ext))
                return BadRequest("Unsupported image type. Allowed: jpg, jpeg, png, webp.");

            // Never trust the client-supplied filename; generate a safe one.
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var relativeDir = Path.Combine("uploads", "members");
            var targetDir = Path.Combine(webRoot, relativeDir);
            Directory.CreateDirectory(targetDir);

            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(targetDir, fileName);
            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var photoUrl = "/" + Path.Combine(relativeDir, fileName).Replace(Path.DirectorySeparatorChar, '/');
            user.UpdateProfile(null, null, photoUrl); // only sets the photo, keeps name fields
            await _userRepo.UpdateAsync(user);
            await _uow.SaveChangesAsync();

            return Ok(new { photoUrl });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
            var user = await _userRepo.GetByIdAsync(Guid.Parse(id));
            if (user == null) return NotFound();

            // verify current password
            if (string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrWhiteSpace(dto.CurrentPassword) || !PasswordHasher.Verify(user.PasswordHash, dto.CurrentPassword))
                return BadRequest("Current password is incorrect.");

            // validate new password strength
            var (ok, reasons) = PasswordPolicy.Validate(dto.NewPassword ?? string.Empty);
            if (!ok) return BadRequest(new { errors = reasons });

            var hash = PasswordHasher.HashPassword(dto.NewPassword!);
            user.SetPasswordHash(hash);
            await _userRepo.UpdateAsync(user);
            await _uow.SaveChangesAsync();
            return NoContent();
        }
    }

    public class UpdateProfileDto
    {
        public string? UserName { get; set; }
        public string? FullName { get; set; }
        public string? PhotoUrl { get; set; }
    }

    public class ChangePasswordDto
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }
}

