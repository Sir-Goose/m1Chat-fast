using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using m1Chat.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using m1Chat.Services;

namespace m1Chat.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly ChatDbContext _db;

    public UserController(ChatDbContext db)
    {
        _db = db;
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        return Ok(new { email });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetUserStats()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (email == null)
        {
            return Unauthorized();
        }

        var user = await _db.Users
            .Include(u => u.Chats)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            return NotFound(new { error = "User not found." });
        }

        int totalChats = user.Chats.Count;

        int totalMessages = 0;
        foreach (var chat in user.Chats)
        {
            try
            {
                var messages = JsonSerializer.Deserialize<List<ChatMessageDto>>(chat.HistoryJson);
                totalMessages += messages?.Count ?? 0;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error deserializing chat history for chat {chat.Id}: {ex.Message}");
            }
        }

        return Ok(new UserStatsResponseDto
        {
            TotalChats = totalChats,
            TotalMessages = totalMessages,
        });
    }

    public class UserStatsResponseDto
    {
        public int TotalChats { get; set; }
        public int TotalMessages { get; set; }
    }
}
