using Backend.Api.Data;
using Backend.Api.Models.Dtos.User;
using Backend.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public UserController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            var users = await _dbContext.User.ToListAsync();

            var result = users.Select(user => new UserDto 
            { 
                Id = user.Id, 
                Email = user.Email
            });

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            var user = await _dbContext.User.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var dto = new UserDto 
            {
                Id = user.Id,
                Email = user.Email
            };

            return dto;
        }

        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var emailExists = await _dbContext.User.AnyAsync(u => u.Email == dto.Email);
            if (emailExists)
            {
                return Conflict("Email already exists.");
            }

            var user = new User
            {
                Email = dto.Email,
                PasswordHash = HashPassword(dto.Password)
            };

            _dbContext.User.Add(user);
            await _dbContext.SaveChangesAsync();

            var result = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UpdateUserDto dto) 
        {
            if(!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }
            
            var user = await _dbContext.User.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var emailExists = await _dbContext.User.AnyAsync(u => u.Email == dto.Email);
            if (emailExists)
            {
                return Conflict("Email already exists.");
            }

            user.Email = dto.Email;
            user.PasswordHash = HashPassword(dto.Password);

            await _dbContext.SaveChangesAsync();

            var result = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _dbContext.User.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            _dbContext.User.Remove(user);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private string HashPassword(string password)
        {
            return password + "_hashed"; // example
        }
    }
}
