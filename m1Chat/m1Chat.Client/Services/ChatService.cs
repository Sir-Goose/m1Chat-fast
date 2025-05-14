using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using m1Chat.Client.Services;

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
            return await _http.GetFromJsonAsync<List<ChatSummary>>("api/chats")
                   ?? new List<ChatSummary>();
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

        // ---- DTOs / Models ----
        public record ChatSummary(Guid Id, string Name, string Model, DateTime LastUpdatedAt);

        public record ChatMessageDto(string Role, string Content);

        public record ChatHistory(Guid Id, string Name, string Model, ChatMessageDto[] Messages);

        public record CreateChatRequest(string Name, string Model, ChatMessageDto[] Messages);

        public record CreateChatResponse(Guid id);

        public record UpdateChatRequest(string Name, string Model, ChatMessageDto[] Messages);
    }
}