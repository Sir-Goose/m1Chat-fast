// In m1Chat.Client/Services/ChatCompletionService.cs
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;


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
                Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("data: "))
                {
                    var chunk = line.Substring("data: ".Length);
                    yield return chunk;
                }
            }
        }
    }
}