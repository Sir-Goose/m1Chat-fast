using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace m1Chat.Client.Services
{
    public class ChatMessage
    {
        public required string Role { get; set; }
        public required string Content { get; set; }
        public List<Guid>? FileIds { get; set; }
    }

    public class ChatCompletionService
    {
        private readonly SignalRService _signalRService;
        private readonly HttpClient _http;

        public ChatCompletionService(
            HttpClient http,
            SignalRService signalRService)
        {
            _http = http;
            _signalRService = signalRService;
        }

        public async Task StreamCompletion(
            List<ChatMessage> messages,
            string model,
            string reasoningEffort,
            Action<string> onChunk,
            Action onComplete,
            Action<string> onError)
        {
            try
            {
                await _signalRService.InitializeAsync();
                var connectionId = _signalRService.GetConnectionId();

                var payload = new
                {
                    model,
                    reasoningEffort,
                    messages = messages
                        .Select(m => new
                        {
                            role = m.Role,
                            content = m.Content,
                            fileIds = m.FileIds
                        })
                        .ToArray()
                };

                // Declare handlers
                void ChunkHandler(string chunk) => onChunk(chunk);

                void CompleteHandler()
                {
                    onComplete?.Invoke();
                    UnregisterHandlers();
                }

                void ErrorHandler(string error)
                {
                    onError?.Invoke(error);
                    UnregisterHandlers();
                }

                // Register handlers
                void RegisterHandlers()
                {
                    _signalRService.OnChunkReceived += ChunkHandler;
                    _signalRService.OnStreamCompleted += CompleteHandler;
                    _signalRService.OnStreamError += ErrorHandler;
                }

                // Unregister handlers
                void UnregisterHandlers()
                {
                    _signalRService.OnChunkReceived -= ChunkHandler;
                    _signalRService.OnStreamCompleted -= CompleteHandler;
                    _signalRService.OnStreamError -= ErrorHandler;
                }

                // Register handlers for this session
                RegisterHandlers();

                // Start streaming
                var response = await _http.PostAsJsonAsync(
                    $"/api/completions/stream?connectionId={connectionId}",
                    payload);

                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Failed to start streaming: {ex.Message}");
            }
        }
    }
}