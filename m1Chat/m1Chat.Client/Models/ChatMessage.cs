// ChatMessage.cs
namespace m1Chat.Client.Models;

public record ClientChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public List<Guid> FileIds { get; set; } = new();
    public bool IsStreaming { get; set; }
}