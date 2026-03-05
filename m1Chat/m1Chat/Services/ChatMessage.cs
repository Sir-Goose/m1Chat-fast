namespace m1Chat.Services
{
    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class ChatHistoryRequest
    {
        public List<ChatMessage> Messages { get; set; } = new();
    }
}
