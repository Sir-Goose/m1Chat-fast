using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace m1Chat.Client.Services
{
    public class ChatCacheService
    {
        private readonly ConcurrentDictionary<Guid, ChatService.ChatHistory> _cache = new();
        private readonly ChatService _chatService;

        public ChatCacheService(ChatService chatService)
        {
            _chatService = chatService;
        }

        public async Task PrefetchChatAsync(Guid chatId)
        {
            if (!_cache.ContainsKey(chatId))
            {
                try
                {
                    var chat = await _chatService.GetChatAsync(chatId);
                    if (chat != null)
                    {
                        _cache[chatId] = chat;
                    }
                }
                catch
                {
                    // Gracefully handle errors
                }
            }
        }

        public ChatService.ChatHistory? GetChatIfCached(Guid chatId)
        {
            _cache.TryGetValue(chatId, out var chat);
            return chat;
        }

        public void InvalidateCache(Guid chatId)
        {
            _cache.TryRemove(chatId, out _);
        }
    }
}