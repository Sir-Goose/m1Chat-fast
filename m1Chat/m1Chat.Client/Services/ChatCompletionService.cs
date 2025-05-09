using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.IO;

namespace m1Chat.Client.Services
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
    
    public class ChatCompletionService
    {
        private readonly HttpClient _httpClient;

        public ChatCompletionService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async IAsyncEnumerable<string> StreamCompletionAsync(List<ChatMessage> messages)
        {
            var request = new ChatHistoryRequest { Messages = messages };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/completions/stream")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                )
            };

            var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead
            );

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string? chunk = null;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("content", out var contentProp))
                    {
                        chunk = contentProp.GetString();
                    }
                }
                catch
                {
                    // Ignore malformed lines
                }

                if (!string.IsNullOrEmpty(chunk))
                    yield return chunk;
            }
        }
    }
}
