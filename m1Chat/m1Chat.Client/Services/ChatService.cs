using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace m1Chat.Client.Services
{
    public class ChatService
    {
        private readonly HttpClient _http;

        public ChatService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<ChatSummary>> GetChatsAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/chats");
                var raw = await response.Content.ReadAsStringAsync();

                Console.WriteLine("----- RAW RESPONSE FROM api/chats -----");
                Console.WriteLine(raw);
                Console.WriteLine("----- END RAW RESPONSE -----");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Non-success status code: {response.StatusCode}");
                    return new List<ChatSummary>();
                }

                var chats = JsonSerializer.Deserialize<List<ChatSummary>>(
                    raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                return chats ?? new List<ChatSummary>();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON error in GetChatsAsync: {ex.Message}");
                return new List<ChatSummary>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in GetChatsAsync: {ex.Message}");
                return new List<ChatSummary>();
            }
        }

        public async Task<ChatHistory> GetChatAsync(Guid id)
        {
            return await _http.GetFromJsonAsync<ChatHistory>($"api/chats/{id}");
        }

        public async Task<Guid> CreateChatAsync(CreateChatRequest req)
        {
            var resp = await _http.PostAsJsonAsync("api/chats", req);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<CreateChatResponse>();
            return body!.id;
        }

        public async Task UpdateChatAsync(Guid id, UpdateChatRequest req)
        {
            var resp = await _http.PutAsJsonAsync($"api/chats/{id}", req);
            resp.EnsureSuccessStatusCode();
        }

        public async Task DeleteChatAsync(Guid id)
        {
            var resp = await _http.DeleteAsync($"api/chats/{id}");
            resp.EnsureSuccessStatusCode();
        }
        
        public async Task PinChatAsync(Guid id, bool isPinned)
        {
            var resp = await _http.PatchAsJsonAsync($"api/chats/{id}/pin", new PinChatRequest(isPinned));
            resp.EnsureSuccessStatusCode();
        }

        // ---- DTOs / Models ----
        public record ChatSummary(Guid Id, string Name, string Model, DateTime LastUpdatedAt, bool IsPinned);

        public record ChatMessageDto(string Role, string Content, List<Guid>? FileIds = null);

        public record ChatHistory(Guid Id, string Name, string Model, ChatMessageDto[] Messages, bool IsPinned);

        public record CreateChatRequest(string Name, string Model, ChatMessageDto[] Messages, bool IsPinned = false);

        public record CreateChatResponse(Guid id);

        public record UpdateChatRequest(string Name, string Model, ChatMessageDto[] Messages, bool IsPinned);
        public record PinChatRequest(bool IsPinned);
    }
}