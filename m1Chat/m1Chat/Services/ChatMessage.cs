namespace m1Chat.Services
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class ChatHistoryRequest
    {
        public List<ChatMessage> Messages { get; set; }
    }
}