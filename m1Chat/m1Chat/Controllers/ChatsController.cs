using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using m1Chat.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace m1Chat.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatsController : ControllerBase
    {
        private readonly ChatDbContext _db;

        public ChatsController(ChatDbContext db)
        {
            _db = db;
        }

        // GET api/chats
        [HttpGet]
        public async Task<IActionResult> GetChats()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var user = await _db.Users
                .Include(u => u.Chats)
                .FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound();

            var list = user.Chats
                .OrderByDescending(c => c.LastUpdatedAt)
                .Select(c => new ChatSummaryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Model = c.Model,
                    LastUpdatedAt = c.LastUpdatedAt
                })
                .ToList();

            return Ok(list);
        }

        // GET api/chats/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetChat(Guid id)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var chat = await _db.Chats
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id && c.User.Email == email);
            if (chat == null) return NotFound();

            var messages = JsonSerializer.Deserialize<List<ChatMessageDto>>(chat.HistoryJson)
                           ?? new List<ChatMessageDto>();

            var resp = new ChatHistoryDto
            {
                Id = chat.Id,
                Name = chat.Name,
                Model = chat.Model,
                Messages = messages
            };
            return Ok(resp);
        }

        // POST api/chats
        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatDto dto)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound();

            var chat = new Chat
            {
                UserId = user.Id,
                Name = dto.Name,
                Model = dto.Model,
                HistoryJson = JsonSerializer.Serialize(dto.Messages)
            };
            _db.Chats.Add(chat);
            await _db.SaveChangesAsync();

            return Ok(new { id = chat.Id });
        }

        // PUT api/chats/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateChat(Guid id, [FromBody] UpdateChatDto dto)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var chat = await _db.Chats
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id && c.User.Email == email);
            if (chat == null) return NotFound();

            chat.Name = dto.Name;
            chat.Model = dto.Model;
            chat.HistoryJson = JsonSerializer.Serialize(dto.Messages);
            chat.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return NoContent();
        }
        
        // DELETE api/chats/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteChat(Guid id)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var chat = await _db.Chats
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id && c.User.Email == email);

            if (chat == null) return NotFound();

            _db.Chats.Remove(chat);
            await _db.SaveChangesAsync();

            return NoContent();
        }


        // ---- DTOs ----
        public record ChatMessageDto(string Role, string Content);

        public record ChatSummaryDto
        {
            public Guid Id { get; init; }
            public string Name { get; init; }
            public string Model { get; init; }
            public DateTime LastUpdatedAt { get; init; }
        }

        public record ChatHistoryDto
        {
            public Guid Id { get; init; }
            public string Name { get; init; }
            public string Model { get; init; }
            public List<ChatMessageDto> Messages { get; init; }
        }

        public record CreateChatDto
        {
            public string Name { get; init; }
            public string Model { get; init; }
            public ChatMessageDto[] Messages { get; init; }
        }

        public record UpdateChatDto
        {
            public string Name { get; init; }
            public string Model { get; init; }
            public ChatMessageDto[] Messages { get; init; }
        }
    }
}
