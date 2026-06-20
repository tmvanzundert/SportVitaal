using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.Enums;
using SportVitaal.Domain.Entities;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly SportVitaal.Domain.Repositories.IUnitOfWork _uow;
        private readonly IConfiguration _config;

        public AuthController(IUserRepository userRepository, SportVitaal.Domain.Repositories.IUnitOfWork uow, IConfiguration config)
        {
            _userRepository = userRepository;
            _uow = uow;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var existing = await _userRepository.GetByEmailAsync(dto.Email);
            if (existing != null) return Conflict("User with this email already exists.");
            var user = new UserAccount(dto.Email, dto.Role ?? Role.Member);
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                var hash = SportVitaal.Application.Services.PasswordHasher.HashPassword(dto.Password);
                user.SetPasswordHash(hash);
            }

            if (!string.IsNullOrWhiteSpace(dto.UserName) || !string.IsNullOrWhiteSpace(dto.FullName))
                user.UpdateProfile(dto.UserName, dto.FullName, null);

            await _userRepository.AddAsync(user);
            await _uow.SaveChangesAsync();

            return CreatedAtAction(nameof(Register), new { id = user.Id }, new { user.Id, user.Email });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(user.PasswordHash) || !SportVitaal.Application.Services.PasswordHasher.Verify(user.PasswordHash, dto.Password))
                return Unauthorized();

            var jwtKey = _config["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var displayName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName!
                : !string.IsNullOrWhiteSpace(user.UserName) ? user.UserName!
                : user.Email;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Name, displayName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(new { token = jwt });
        }
    }

    public class RegisterDto
    {
        public string Email { get; set; } = null!;
        public Role? Role { get; set; }
        public string? Password { get; set; }
        public string? FullName { get; set; }
        public string? UserName { get; set; }
    }

    public class LoginDto
    {
        public string Email { get; set; } = null!;
        public string? Password { get; set; }
    }
}

