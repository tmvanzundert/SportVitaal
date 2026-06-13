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
        private readonly IUserRepository _userRepo;
        private readonly SportVitaal.Domain.Repositories.IUnitOfWork _uow;

        public UsersController(IUserRepository userRepo, SportVitaal.Domain.Repositories.IUnitOfWork uow)
        {
            _userRepo = userRepo;
            _uow = uow;
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

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
            var user = await _userRepo.GetByIdAsync(Guid.Parse(id));
            if (user == null) return NotFound();
            return Ok(new { user.Id, user.Email, user.UserName, user.FullName, user.PhotoUrl, Role = user.Role.ToString() });
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

