namespace m1Chat.Client.Models;

public class ClientChatMessage
{
    public string UserId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty; // Or use UserId to lookup Author Name/Avatar
    public DateTime Timestamp { get; set; }
    public string Text { get; set; } = string.Empty;
    // Add other properties like IsRead, etc. if needed
}