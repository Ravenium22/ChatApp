using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Backend.Data;
using Backend.Models;
using Backend.DTOs;
using Backend.Services;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly JwtService _jwtService;

        public UserController(ApplicationDbContext context, PasswordService passwordService, JwtService jwtService)
        {
            _context = context;
            _passwordService = passwordService;
            _jwtService = jwtService;
        }

        // POST: api/users/register
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(UserRegisterDto registerDto)
        {
            // Email zaten var mı kontrol et
            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
            {
                return BadRequest("Email zaten kullanılıyor");
            }

            // Username zaten var mı kontrol et
            if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username))
            {
                return BadRequest("Username zaten kullanılıyor");
            }

            // Yeni user oluştur
            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = _passwordService.HashPassword(registerDto.Password),
                CreatedAt = DateTime.UtcNow,
                IsOnline = false
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // JWT token oluştur
            var token = _jwtService.GenerateToken(user);

            // Response DTO'ya çevir
            var response = new AuthResponseDto
            {
                Token = token,
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    CreatedAt = user.CreatedAt,
                    IsOnline = user.IsOnline,
                    LastSeen = user.LastSeen
                }
            };

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, response);
        }

        // POST: api/users/login
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(UserLoginDto loginDto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null || !_passwordService.VerifyPassword(loginDto.Password, user.PasswordHash))
            {
                return Unauthorized("Email veya şifre hatalı");
            }

            // User'ı online yap
            user.IsOnline = true;
            user.LastSeen = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // JWT token oluştur
            var token = _jwtService.GenerateToken(user);

            var response = new AuthResponseDto
            {
                Token = token,
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    CreatedAt = user.CreatedAt,
                    IsOnline = user.IsOnline,
                    LastSeen = user.LastSeen
                }
            };

            return Ok(response);
        }

        // GET: api/users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
        {
            var users = await _context.Users.ToListAsync();
            
            var responseDtos = users.Select(u => new UserResponseDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                CreatedAt = u.CreatedAt,
                IsOnline = u.IsOnline,
                LastSeen = u.LastSeen
            }).ToList();

            return Ok(responseDtos);
        }

        // GET: api/users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserResponseDto>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var responseDto = new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                IsOnline = user.IsOnline,
                LastSeen = user.LastSeen
            };

            return Ok(responseDto);
        }

        // POST: api/users/logout
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult> Logout()
        {
            var principal = HttpContext.User;
            var userId = _jwtService.GetUserIdFromToken(principal);

            if (userId.HasValue)
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user != null)
                {
                    user.IsOnline = false;
                    user.LastSeen = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { message = "Başarıyla çıkış yapıldı" });
        }

        // GET: api/users/profile
        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<UserResponseDto>> GetProfile()
        {
            var principal = HttpContext.User;
            var userId = _jwtService.GetUserIdFromToken(principal);

            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId.Value);

            if (user == null)
            {
                return NotFound();
            }

            var responseDto = new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                IsOnline = user.IsOnline,
                LastSeen = user.LastSeen
            };

            return Ok(responseDto);
        }
    }
}