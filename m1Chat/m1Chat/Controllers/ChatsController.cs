using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using m1Chat.Data;
using m1Chat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fastenshtein;

namespace m1Chat.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatsController : ControllerBase
    {
        private readonly ChatDbContext _db;
        private readonly FileService _fileService;

        public ChatsController(ChatDbContext db, FileService fileService)
        {
            _db = db;
            _fileService = fileService;
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
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.LastUpdatedAt)
                .Select(c => new ChatSummaryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Model = c.Model,
                    LastUpdatedAt = c.LastUpdatedAt,
                    IsPinned = c.IsPinned
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
                Messages = messages,
                IsPinned = chat.IsPinned
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
                HistoryJson = JsonSerializer.Serialize(dto.Messages),
                IsPinned = dto.IsPinned
            };
            _db.Chats.Add(chat);
            await _db.SaveChangesAsync();

            // Attach files to messages if any
            for (int i = 0; i < dto.Messages.Length; i++)
            {
                var message = dto.Messages[i];
                if (message.FileIds != null && message.FileIds.Any())
                {
                    await _fileService.AttachFilesToMessageAsync(chat.Id, i, message.FileIds.ToList());
                }
            }

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
            chat.IsPinned = dto.IsPinned;

            await _db.SaveChangesAsync();

            // Update file attachments for messages
            for (int i = 0; i < dto.Messages.Length; i++)
            {
                var message = dto.Messages[i];
                if (message.FileIds != null)
                {
                    await _fileService.AttachFilesToMessageAsync(chat.Id, i, message.FileIds.ToList());
                }
            }

            return NoContent();
        }

        [HttpPatch("{id:guid}/pin")]
        public async Task<IActionResult> PinChat(Guid id, [FromBody] PinChatDto dto)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var chat = await _db.Chats
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id && c.User.Email == email);
            if (chat == null) return NotFound();

            chat.IsPinned = dto.IsPinned;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        public record PinChatDto
        {
            public bool IsPinned { get; init; }
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

        [HttpPost("{id:guid}/attach-files")]
        public async Task<IActionResult> AttachFilesToMessage(Guid id, [FromBody] AttachFilesRequest request)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var chat = await _db.Chats
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id && c.User.Email == email);
            if (chat == null) return NotFound();

            await _fileService.AttachFilesToMessageAsync(id, request.MessageIndex, request.FileIds);

            return NoContent();
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchChats([FromQuery] string query)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var user = await _db.Users
                .Include(u => u.Chats)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(query))
                return Ok(new List<ChatSearchResultDto>());

            var results = new List<ChatSearchResultDto>();
            var exactMatchRegex = new Regex($@"\b{Regex.Escape(query)}\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (var chat in user.Chats)
            {
                int score = 0;

                // 1. Check chat name first (highest priority)
                if (exactMatchRegex.IsMatch(chat.Name))
                {
                    score = 100; // Exact name match
                }
                else if (chat.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    score = 70; // Partial name match
                }

                // 2. Only check messages if no name match found
                if (score == 0)
                {
                    var messages = JsonSerializer.Deserialize<List<ChatMessageDto>>(chat.HistoryJson)
                                   ?? new List<ChatMessageDto>();
                    int matchCount = 0;

                    foreach (var message in messages)
                    {
                        if (exactMatchRegex.IsMatch(message.Content))
                        {
                            matchCount++;
                        }
                    }

                    if (matchCount > 0)
                    {
                        // Base score + term frequency bonus
                        score = Math.Min(80, 40 + (matchCount * 5));
                    }
                }

                // Add to results if above threshold
                if (score >= 40)
                {
                    results.Add(new ChatSearchResultDto
                    {
                        Id = chat.Id,
                        Name = chat.Name,
                        Model = chat.Model,
                        LastUpdatedAt = chat.LastUpdatedAt,
                        IsPinned = chat.IsPinned,
                        Score = score
                    });
                }
            }

            return Ok(results
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.LastUpdatedAt));
        }


// Add new DTO at bottom of ChatsController class
        public record ChatSearchResultDto
        {
            public Guid Id { get; init; }
            public string Name { get; init; }
            public string Model { get; init; }
            public DateTime LastUpdatedAt { get; init; }
            public bool IsPinned { get; init; }
            public int Score { get; init; }
        }

        public record AttachFilesRequest(int MessageIndex, List<Guid> FileIds);

        // ---- DTOs ----
        public record ChatMessageDto(string Role, string Content, List<Guid>? FileIds = null);

        public record ChatSummaryDto
        {
            public Guid Id { get; init; }
            public string Name { get; init; }
            public string Model { get; init; }
            public DateTime LastUpdatedAt { get; init; }
            public bool IsPinned { get; init; }
        }

        public record ChatHistoryDto
        {
            public Guid Id { get; init; }
            public string Name { get; init; }
            public string Model { get; init; }
            public List<ChatMessageDto> Messages { get; init; }
            public bool IsPinned { get; init; }
        }

        public record CreateChatDto
        {
            public string Name { get; init; }
            public string Model { get; init; }
            public ChatMessageDto[] Messages { get; init; }
            public bool IsPinned { get; init; }
        }

        public record UpdateChatDto
        {
            public string Name { get; init; }
            public string Model { get; init; }
            public ChatMessageDto[] Messages { get; init; }
            public bool IsPinned { get; init; }
        }
    }
}